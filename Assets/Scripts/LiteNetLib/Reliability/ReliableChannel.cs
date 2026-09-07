#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiteNetLib.Reliability
{

/// <summary>
/// 基于UDP的轻量级可靠传输通道
/// 提供可靠有序和不可靠两种发送模式
/// 每个网络连接应拥有独立的ReliableChannel实例
/// </summary>
public class ReliableChannel
{
    private const byte Flag_Unreliable = 0;
    private const byte Flag_Reliable = 1;
    private const byte Flag_Ack = 2;

    private class PendingMessage
    {
        public uint SequenceNumber;
        public byte[] Data = Array.Empty<byte>();
        public DateTime SendTime;
        public int RetryCount;
    }

    // ---- 发送侧状态（_sendLock 保护） ----
    private readonly object _sendLock = new();
    private readonly ConcurrentDictionary<uint, PendingMessage> _pendingAcks = new();
    private uint _nextSendSequence;

    // ---- 接收侧状态（由 _reorderLock 保护） ----
    private readonly object _reorderLock = new();
    private readonly SortedDictionary<uint, byte[]> _reorderBuffer = new();
    private uint _nextReceiveSequence;

    // ---- 配置 ----
    private readonly TimeSpan _resendTimeout = TimeSpan.FromMilliseconds(200);
    private readonly int _maxRetries = 5;
    private const int HoleSkipThreshold = 10; // 空洞跳过阈值：buffer 累积超过此数量才跳过

    // ---- 回调 ----
    public Action<byte[]>? OnReliableDataReceived;
    public Action<byte[]>? OnUnreliableDataReceived;
    public Action<byte[]>? OnSendRawData;
    public Action<uint, byte[]>? OnSendFailed;
    public Action<string>? OnLog;

    /// <summary>
    /// 发送可靠消息（保证送达、保证顺序）
    /// </summary>
    public void SendReliable(byte[] data)
    {
        byte[] dataCopy = new byte[data.Length];
        Buffer.BlockCopy(data, 0, dataCopy, 0, data.Length);

        uint seq;
        lock (_sendLock)
        {
            seq = _nextSendSequence++;
        }

        var pending = new PendingMessage
        {
            SequenceNumber = seq,
            Data = dataCopy,
            SendTime = DateTime.UtcNow,
            RetryCount = 0
        };

        _pendingAcks.TryAdd(seq, pending);
        byte[] packet = BuildReliablePacket(seq, dataCopy);
        SendRaw(packet);
    }

    /// <summary>
    /// 发送不可靠消息（不保证送达、不保证顺序）
    /// </summary>
    public void SendUnreliable(byte[] data)
    {
        byte[] packet = BuildUnreliablePacket(data);
        SendRaw(packet);
    }

    /// <summary>
    /// 收到原始UDP数据时调用（来自网络线程）
    /// </summary>
    public void OnRawDataReceived(byte[] data)
    {
        if (data.Length < 1) return;

        byte flag = data[0];

        switch (flag)
        {
            case Flag_Unreliable:
                HandleUnreliable(data);
                break;
            case Flag_Reliable:
                HandleReliable(data);
                break;
            case Flag_Ack:
                HandleAck(data);
                break;
        }
    }

    /// <summary>
    /// 驱动重传定时器（每帧调用）
    /// </summary>
    public void Update()
    {
        var now = DateTime.UtcNow;
        var failedMessages = new List<(uint seq, byte[] data)>();

        foreach (var kvp in _pendingAcks)
        {
            var pending = kvp.Value;
            if (now - pending.SendTime > _resendTimeout)
            {
                if (pending.RetryCount >= _maxRetries)
                {
                    failedMessages.Add((kvp.Key, pending.Data));
                }
                else
                {
                    pending.RetryCount++;
                    pending.SendTime = now;
                    byte[] packet = BuildReliablePacket(pending.SequenceNumber, pending.Data);
                    SendRaw(packet);
                    OnLog?.Invoke($"[ReliableChannel] 重传 seq={pending.SequenceNumber} (第{pending.RetryCount}次)");
                }
            }
        }

        foreach (var (seq, data) in failedMessages)
        {
            _pendingAcks.TryRemove(seq, out _);
            OnLog?.Invoke($"[ReliableChannel] 消息 seq={seq} 重试超过{_maxRetries}次，放弃发送");
            OnSendFailed?.Invoke(seq, data);
        }

        // 接收方空洞跳过检测：
        // 如果 reorderBuffer 积累超过 _maxRetries×2 条消息还在等某个序号，
        // 说明发送方已经重试耗尽放弃那个序号了。
        // 跳过空洞，从 buffer 里最早的消息开始投递。
        if (_reorderBuffer.Count >= HoleSkipThreshold)
        {
            lock (_reorderLock)
            {
                if (_reorderBuffer.Count >= HoleSkipThreshold)
                {
                    uint minSeq = uint.MaxValue;
                    foreach (var key in _reorderBuffer.Keys)
                    {
                        if (IsSequenceBefore(key, minSeq))
                            minSeq = key;
                    }

                    // 跳过空洞到 buffer 里最早的消息
                    _nextReceiveSequence = minSeq;
                    OnLog?.Invoke($"[ReliableChannel] 跳过空洞，前进到 seq={minSeq}，buffer 中有 {_reorderBuffer.Count} 条等待");

                    // 投递所有连续的消息
                    while (_reorderBuffer.TryGetValue(_nextReceiveSequence, out byte[]? next))
                    {
                        _reorderBuffer.Remove(_nextReceiveSequence);
                        _nextReceiveSequence++;
                        OnReliableDataReceived?.Invoke(next);
                    }
                }
            }
        }
    }

    // ==================== 内部方法 ====================

    private void SendRaw(byte[] data)
    {
        if (OnSendRawData == null)
        {
            OnLog?.Invoke("[ReliableChannel] 警告：OnSendRawData 未设置，数据被丢弃！");
            return;
        }
        OnSendRawData(data);
    }

    private void HandleUnreliable(byte[] data)
    {
        byte[] body = new byte[data.Length - 1];
        Buffer.BlockCopy(data, 1, body, 0, body.Length);
        OnUnreliableDataReceived?.Invoke(body);
    }

    private void HandleReliable(byte[] data)
    {
        if (data.Length < 5)
        {
            OnLog?.Invoke($"[ReliableChannel] 收到损坏的可靠消息，长度={data.Length}");
            return;
        }

        uint seq = BitConverter.ToUInt32(data, 1);
        byte[] body = new byte[data.Length - 5];
        Buffer.BlockCopy(data, 5, body, 0, body.Length);

        // ACK 在锁外发送（不涉及共享状态）
        SendAck(seq);

        lock (_reorderLock)
        {
            // 使用 RFC 1982 序列号比较处理 uint 回绕
            if (IsSequenceBefore(seq, _nextReceiveSequence))
            {
                OnLog?.Invoke($"[ReliableChannel] 收到重复消息 seq={seq}，已忽略");
                return;
            }

            if (!_reorderBuffer.ContainsKey(seq))
            {
                _reorderBuffer[seq] = body;
            }

            // 按序投递所有连续的消息
            while (_reorderBuffer.TryGetValue(_nextReceiveSequence, out byte[]? next))
            {
                _reorderBuffer.Remove(_nextReceiveSequence);
                _nextReceiveSequence++;
                // 在锁内投递，确保顺序语义
                OnReliableDataReceived?.Invoke(next);
            }
        }
    }

    private void HandleAck(byte[] data)
    {
        if (data.Length < 5)
        {
            OnLog?.Invoke($"[ReliableChannel] 收到损坏的ACK包，长度={data.Length}");
            return;
        }

        uint ackSeq = BitConverter.ToUInt32(data, 1);

        if (_pendingAcks.TryRemove(ackSeq, out _))
        {
            OnLog?.Invoke($"[ReliableChannel] ACK seq={ackSeq} 确认，从队列移除");
        }
        else
        {
            OnLog?.Invoke($"[ReliableChannel] 收到未知ACK seq={ackSeq}（可能已确认或超时）");
        }
    }

    private void SendAck(uint seq)
    {
        byte[] ackPacket = new byte[5];
        ackPacket[0] = Flag_Ack;
        BitConverter.GetBytes(seq).CopyTo(ackPacket, 1);
        SendRaw(ackPacket);
    }

    // ==================== 数据包构造 ====================

    private static byte[] BuildReliablePacket(uint seq, byte[] body)
    {
        byte[] packet = new byte[1 + 4 + body.Length];
        packet[0] = Flag_Reliable;
        BitConverter.GetBytes(seq).CopyTo(packet, 1);
        Buffer.BlockCopy(body, 0, packet, 5, body.Length);
        return packet;
    }

    private static byte[] BuildUnreliablePacket(byte[] body)
    {
        byte[] packet = new byte[1 + body.Length];
        packet[0] = Flag_Unreliable;
        Buffer.BlockCopy(body, 0, packet, 1, body.Length);
        return packet;
    }

    /// <summary>
    /// RFC 1982 序列号比较：判断 s1 是否在序列空间中先于 s2。
    /// 使用补码运算正确处理 uint 回绕，有效范围为差值不超过 2^31-1。
    /// </summary>
    public static bool IsSequenceBefore(uint s1, uint s2)
    {
        return unchecked((int)(s1 - s2)) < 0;
    }
}
}
