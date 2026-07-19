#nullable enable
using System;
using System.Collections.Generic;
using LiteNetLib.Protocol;

namespace LiteNetLib.Dispatch
{

/// <summary>
/// 消息分发器 —— 按消息ID路由到类型化处理器
/// 使用闭包在注册时捕获泛型类型，分发时零反射开销
/// </summary>
public class MessageDispatcher
{
    public delegate void MessageHandler<T>(Guid clientId, T message) where T : class;

    private readonly Dictionary<int, Action<Guid, byte[]>> _handlers = new();

    public Action<string>? OnLog;

    /// <summary>
    /// 注册消息处理器。闭包在此时捕获泛型类型T，后续Dispatch无需反射。
    /// </summary>
    public void RegisterHandler<T>(int messageId, MessageHandler<T> handler) where T : class
    {
        if (_handlers.ContainsKey(messageId))
        {
            OnLog?.Invoke($"[MessageDispatcher] 警告：消息ID {messageId} 重复注册，将被覆盖");
        }

        _handlers[messageId] = (clientId, data) =>
        {
            T message = ProtobufSerializer.Deserialize<T>(data);
            handler(clientId, message);
        };

        OnLog?.Invoke($"[MessageDispatcher] 注册消息处理器: ID={messageId}, 类型={typeof(T).Name}");
    }

    /// <summary>
    /// 取消注册消息处理器
    /// </summary>
    public void UnregisterHandler(int messageId)
    {
        _handlers.Remove(messageId);
    }

    /// <summary>
    /// 分发消息到已注册的处理器。零反射，直接委托调用。
    /// </summary>
    public void Dispatch(Guid clientId, int messageId, byte[] data)
    {
        if (_handlers.TryGetValue(messageId, out var handler))
        {
            try
            {
                handler(clientId, data);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[MessageDispatcher] 分发消息ID={messageId}时出错: {ex.Message}");
            }
        }
        else
        {
            OnLog?.Invoke($"[MessageDispatcher] 未注册的消息ID: {messageId}");
        }
    }

    // 将消息ID和Protobuf对象打包：[4字节消息ID][Protobuf消息体]
    public static byte[] PackMessage(int messageId, object message)
    {
        byte[] body = ProtobufSerializer.Serialize(message);
        return PackMessage(messageId, body);
    }

    // 将消息ID和已序列化的字节打包：[4字节消息ID][body]
    public static byte[] PackMessage(int messageId, byte[] body)
    {
        byte[] packet = new byte[4 + body.Length];
        BitConverter.GetBytes(messageId).CopyTo(packet, 0);
        Buffer.BlockCopy(body, 0, packet, 4, body.Length);
        return packet;
    }

    // 从打包的字节数组中拆解出消息ID和消息体
    /// </summary>
    public static (int messageId, byte[] body) UnpackMessage(byte[] packet)
    {
        int messageId = BitConverter.ToInt32(packet, 0);
        byte[] body = new byte[packet.Length - 4];
        Buffer.BlockCopy(packet, 4, body, 0, body.Length);
        return (messageId, body);
    }
}
}
