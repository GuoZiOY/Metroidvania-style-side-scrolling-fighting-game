#nullable enable
using System;
using System.Net;
using LiteNetLib.Reliability;

namespace LiteNetLib.Connection
{

/// <summary>
/// 表示一个已连接的客户端，拥有独立的可靠传输通道
/// </summary>
public class ClientConnection
{
    // 整型客户端 ID（由 ConnectionManager 自增分配，供 IGameTransport 用）
    public int ClientId { get; set; }

    /// <summary>
    /// 全局唯一的连接标识符
    /// </summary>
    public Guid ConnectionId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// 客户端的网络端点
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; init; }

    /// <summary>
    /// 当前连接状态
    /// </summary>
    public ConnectionState State { get; set; } = ConnectionState.Connecting;

    /// <summary>
    /// 最后一次收到数据的时间（用于心跳超时检测）
    /// </summary>
    public DateTime LastHeartbeatTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 连接建立时间
    /// </summary>
    public DateTime ConnectedTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 此连接专属的可靠传输通道。每个连接拥有独立的序列号空间和重传队列。
    /// </summary>
    public ReliableChannel Channel { get; }

    public ClientConnection(IPEndPoint remoteEndPoint)
    {
        RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
        Channel = new ReliableChannel();
    }
}
}
