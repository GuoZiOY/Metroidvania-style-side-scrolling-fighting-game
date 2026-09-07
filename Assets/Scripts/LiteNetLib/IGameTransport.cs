using System;
namespace LiteNetLib
{

// 传输层抽象接口。
// 所有上层代码（NetworkManager、游戏逻辑）依赖此接口，
// 不依赖底层传输实现（UDP / Steam P2P 等）。
//
// 实现类：
//   LiteNetTransport —— 基于 ReliableChannel + ConnectionManager + NetworkClient
//   SteamTransport   —— 基于 Steamworks P2P（后续实现）
public interface IGameTransport
{
    // ==================== 服务端接口 ====================

    // 启动服务端，开始监听指定端口
    bool StartServer(int port);

    // 停止服务端
    void StopServer();

    // 向指定客户端发送数据
    void SendToClient(int connectionId, byte[] data, bool reliable);

    // 广播给所有已连接客户端（可排除指定客户端）
    void Broadcast(byte[] data, bool reliable, int excludeId = -1);

    // ---- 服务端事件 ----

    // 新客户端连接
    event Action<int> OnServerClientConnected;

    // 客户端断开连接
    event Action<int> OnServerClientDisconnected;

    // 收到客户端发来的数据
    event Action<int, byte[]> OnServerDataReceived;

    // ==================== 客户端接口 ====================

    // 作为普通客户端连接到服务端
    bool Connect(string address, int port);

    // 作为本地客户端连接（Host 模式，不走网络）
    bool ConnectAsLocal();

    // 断开客户端连接
    void Disconnect();

    // 发送数据到服务端
    void SendToServer(byte[] data, bool reliable);

    // 是否为本地环回连接（Host 模式）
    bool IsLocalConnection { get; }

    // ---- 客户端事件 ----

    // 客户端已连接到服务端
    event Action OnClientConnected;

    // 客户端已断开连接
    event Action OnClientDisconnected;

    // 收到服务端发来的数据
    event Action<byte[]> OnClientDataReceived;

    // ==================== 通用 ====================

    // 每帧调用，驱动心跳检测和重传定时器
    void Update();

    // 服务端是否正在运行
    bool IsServerRunning { get; }

    // 客户端是否已连接
    bool IsConnected { get; }

    // 是否处于 Host 模式（既是服务端又是客户端）
    bool IsHostMode { get; }
}
}
