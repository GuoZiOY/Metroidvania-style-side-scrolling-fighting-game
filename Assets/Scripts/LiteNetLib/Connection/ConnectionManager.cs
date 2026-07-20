#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using LiteNetLib.Reliability;

namespace LiteNetLib.Connection
{

/// <summary>
/// 服务端连接管理器 —— 负责客户端连接的生命周期管理
/// 每个客户端连接拥有独立的 ReliableChannel，确保序列号和ACK状态隔离
/// </summary>
public class ConnectionManager
{
    // 主连接字典：ConnectionId → ClientConnection
    private readonly ConcurrentDictionary<Guid, ClientConnection> _connections = new();

    // 端点索引字典：IP:Port → ConnectionId，实现 O(1) 查找
    private readonly ConcurrentDictionary<string, Guid> _epToId = new();

    // 整型客户端 ID 自增分配
    private int nextClientId = 1;

    // ---- Host 模式本地环回 ----
    private Action<byte[]>? localClientCallback;
    private int localClientId = -1;

    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _timeoutThreshold = TimeSpan.FromSeconds(10);
    private DateTime _lastHeartbeatCheck = DateTime.UtcNow;
    private DateTime _lastHeartbeatSend = DateTime.UtcNow;

    // ---- 回调事件 ----
    public Action<ClientConnection>? OnClientConnected;
    public Action<ClientConnection>? OnClientDisconnected;
    public Action<ClientConnection, byte[]>? OnMessageReceived;
    public Action<IPEndPoint, byte[]>? OnSendToClient;
    public Action<string>? OnLog;

    /// <summary>
    /// 接受新客户端连接，为其创建独立的 ReliableChannel 并绑定回调
    /// </summary>
    public ClientConnection AddConnection(IPEndPoint remoteEndPoint)
    {
        var conn = new ClientConnection(remoteEndPoint)
        {
            ClientId = Interlocked.Increment(ref nextClientId)
        };

        // 将 Channel 的发送回调绑定到此客户端的专属端点
        conn.Channel.OnSendRawData = (data) =>
        {
            OnSendToClient?.Invoke(conn.RemoteEndPoint, data);
        };

        // 可靠消息到达 → 携带真实 ConnectionId 投递给上层
        conn.Channel.OnReliableDataReceived = (data) =>
        {
            OnMessageReceived?.Invoke(conn, data);
        };

        // 不可靠消息到达 → 携带真实 ConnectionId 投递给上层
        conn.Channel.OnUnreliableDataReceived = (data) =>
        {
            OnMessageReceived?.Invoke(conn, data);
        };

        // 发送失败通知
        conn.Channel.OnSendFailed = (seq, data) =>
        {
            OnLog?.Invoke($"[ConnectionManager] 客户端 {conn.ConnectionId} 消息 seq={seq} 发送失败");
        };

        // 日志转发
        conn.Channel.OnLog = (msg) => OnLog?.Invoke(msg);

        _connections.TryAdd(conn.ConnectionId, conn);
        _epToId.TryAdd(EndpointKey(remoteEndPoint), conn.ConnectionId);
        conn.State = ConnectionState.Connected;

        OnLog?.Invoke($"[ConnectionManager] 新客户端连接: {conn.ConnectionId} [{remoteEndPoint}]，当前在线: {_connections.Count}");
        OnClientConnected?.Invoke(conn);
        return conn;
    }

    /// <summary>
    /// 通过端点查找连接（O(1)）
    /// </summary>
    public ClientConnection? FindConnection(IPEndPoint remoteEndPoint)
    {
        if (_epToId.TryGetValue(EndpointKey(remoteEndPoint), out var id))
        {
            _connections.TryGetValue(id, out var conn);
            return conn;
        }
        return null;
    }

    /// <summary>
    /// 通过连接ID查找连接（O(1)）
    /// </summary>
    public ClientConnection? FindConnectionById(Guid connectionId)
    {
        _connections.TryGetValue(connectionId, out var conn);
        return conn;
    }

    /// <summary>
    /// 移除客户端连接
    /// </summary>
    public void RemoveConnection(Guid connectionId)
    {
        if (_connections.TryRemove(connectionId, out var conn))
        {
            _epToId.TryRemove(EndpointKey(conn.RemoteEndPoint), out _);
            conn.State = ConnectionState.Disconnected;
            OnLog?.Invoke($"[ConnectionManager] 客户端断开: {connectionId}，当前在线: {_connections.Count}");
            OnClientDisconnected?.Invoke(conn);
        }
    }

    // ==================== Host 模式本地客户端 ====================

    // 连接本地客户端（Host 模式）。不走 UDP，直接内存传递。
    public int ConnectLocalClient(Action<byte[]> sendToLocalCallback)
    {
        localClientCallback = sendToLocalCallback;
        localClientId = Interlocked.Increment(ref nextClientId);
        OnLog?.Invoke($"[ConnectionManager] 本地客户端已连接（Host 环回），ID={localClientId}");
        return localClientId;
    }

    // 断开本地客户端
    public void DisconnectLocalClient()
    {
        OnLog?.Invoke($"[ConnectionManager] 本地客户端已断开");
        localClientCallback = null;
        localClientId = -1;
    }

    // 是否为本地客户端 ID
    public bool IsLocalClient(int clientId) => clientId == localClientId;

    /// <summary>
    /// 更新客户端心跳时间（收到任何数据时调用）
    /// </summary>
    public void UpdateHeartbeat(Guid connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
        {
            conn.LastHeartbeatTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 向指定客户端可靠发送数据
    /// </summary>
    public void SendTo(Guid connectionId, byte[] data)
    {
        if (_connections.TryGetValue(connectionId, out var conn)
            && conn.State == ConnectionState.Connected)
        {
            conn.Channel.SendReliable(data);
        }
    }

    /// <summary>
    /// 向指定客户端不可靠发送数据
    /// </summary>
    public void SendToUnreliable(Guid connectionId, byte[] data)
    {
        if (_connections.TryGetValue(connectionId, out var conn)
            && conn.State == ConnectionState.Connected)
        {
            conn.Channel.SendUnreliable(data);
        }
    }

    /// <summary>
    /// 向指定客户端直接发送原始字节（不经过可靠通道），用于广播数据
    /// </summary>
    public void SendRaw(Guid connectionId, byte[] data)
    {
        if (_connections.TryGetValue(connectionId, out var conn)
            && conn.State == ConnectionState.Connected)
        {
            OnSendToClient?.Invoke(conn.RemoteEndPoint, data);
        }
    }

    public void Broadcast(byte[] data, Guid excludeId = default, bool includeLocal = true)
    {
        foreach (var kvp in _connections)
        {
            if (kvp.Key == excludeId) continue;
            if (kvp.Value.State == ConnectionState.Connected)
            {
                kvp.Value.Channel.SendReliable(data);
            }
        }

        if (includeLocal && localClientCallback != null)
            localClientCallback.Invoke(data);
    }

    public void BroadcastUnreliable(byte[] data, Guid excludeId = default, bool includeLocal = true)
    {
        foreach (var kvp in _connections)
        {
            if (kvp.Key == excludeId) continue;
            if (kvp.Value.State == ConnectionState.Connected)
            {
                kvp.Value.Channel.SendUnreliable(data);
            }
        }

        if (includeLocal && localClientCallback != null)
            localClientCallback.Invoke(data);
    }

    /// <summary>
    /// 驱动所有连接的心跳检测和重传定时器（每帧调用）
    /// </summary>
    public void Update()
    {
        var now = DateTime.UtcNow;

        // 驱动每个连接的 ReliableChannel.Update()
        foreach (var kvp in _connections)
        {
            if (kvp.Value.State == ConnectionState.Connected)
            {
                kvp.Value.Channel.Update();
            }
        }

        // 心跳超时检测（每秒一次）
        if (now - _lastHeartbeatCheck > _heartbeatInterval)
        {
            _lastHeartbeatCheck = now;

            var timeoutList = new List<Guid>();
            foreach (var kvp in _connections)
            {
                if (now - kvp.Value.LastHeartbeatTime > _timeoutThreshold)
                {
                    timeoutList.Add(kvp.Key);
                }
            }

            foreach (var id in timeoutList)
            {
                OnLog?.Invoke($"[ConnectionManager] 客户端 {id} 心跳超时，断开连接");
                RemoveConnection(id);
            }
        }

        // 主动心跳发送（每秒一次）
        if (now - _lastHeartbeatSend > _heartbeatInterval)
        {
            _lastHeartbeatSend = now;
            SendHeartbeats();
        }
    }

    public int OnlineCount => _connections.Count;

    public IEnumerable<ClientConnection> GetAllConnections()
    {
        return _connections.Values.Where(c => c.State == ConnectionState.Connected);
    }

    // ==================== 内部方法 ====================

    /// <summary>
    /// 向所有已连接客户端发送最小心跳包，维持连接活跃
    /// </summary>
    private void SendHeartbeats()
    {
        // 心跳包：Flag_Unreliable 避免ACK开销 + 一个字节占位
        byte[] heartbeat = new byte[] { 0, 0, 0, 0, 0 };
        heartbeat[0] = 0; // Flag_Unreliable

        foreach (var kvp in _connections)
        {
            if (kvp.Value.State == ConnectionState.Connected)
            {
                OnSendToClient?.Invoke(kvp.Value.RemoteEndPoint, heartbeat);
            }
        }
    }

    private static string EndpointKey(IPEndPoint ep)
    {
        return $"{ep.Address}:{ep.Port}";
    }
}
}
