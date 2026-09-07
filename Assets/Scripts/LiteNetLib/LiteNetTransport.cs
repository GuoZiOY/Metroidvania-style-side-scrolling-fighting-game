#nullable enable
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using LiteNetLib.Connection;

namespace LiteNetLib
{

// IGameTransport 的 LiteNetLib 实现。
// 基于自研的 ReliableChannel（UDP）+ ConnectionManager（服务端）+ NetworkClient（客户端）。
// 支持普通模式（纯服务端/纯客户端）和 Host 模式（服务端+本地客户端环回）。
public class LiteNetTransport : IGameTransport
{
    private ConnectionManager? server;
    private NetworkClient? client;
    private bool isHostMode;

    // 上次错误的详细信息
    public string? LastErrorMessage { get; private set; }

    // Guid(ConnectionManager 内部使用) ↔ int(IGameTransport 使用) 映射
    private readonly ConcurrentDictionary<Guid, int> guidToId = new();
    private readonly ConcurrentDictionary<int, Guid> idToGuid = new();
    private int localClientId = -1;

    private int nextClientId = 1001;

    // 客户端数据队列（后台线程 → 主线程转发）
    private readonly ConcurrentQueue<byte[]> clientReceiveQueue = new();

    // 服务端数据队列（后台线程 → 主线程转发）
    private readonly ConcurrentQueue<(int, byte[])> serverReceiveQueue = new();

    // 服务端连接事件队列（后台线程 → 主线程转发）
    private readonly ConcurrentQueue<System.Action> serverEventQueue = new();

    // ==================== 事件 ====================

    public event Action<int>? OnServerClientConnected;
    public event Action<int>? OnServerClientDisconnected;
    public event Action<int, byte[]>? OnServerDataReceived;
    public event Action? OnClientConnected;
    public event Action? OnClientDisconnected;
    public event Action<byte[]>? OnClientDataReceived;

    // ==================== 状态 ====================

    public bool IsServerRunning => server != null;
    public bool IsConnected => client != null && client.State == ConnectionState.Connected;
    public bool IsHostMode => isHostMode;
    public bool IsLocalConnection => client is { IsLocalMode: true };

    // ==================== 服务端方法 ====================

    public bool StartServer(int port)
    {
        if (server != null)
            throw new InvalidOperationException("服务端已在运行中");

        server = new ConnectionManager();
        server.OnLog = msg => { };

        // 收到远端客户端消息 → 入队，主线程处理
        server.OnMessageReceived = (conn, data) =>
        {
            if (guidToId.TryGetValue(conn.ConnectionId, out int cid))
                serverReceiveQueue.Enqueue((cid, data));
        };

        // 新客户端连接 → 分配 int ID（字典操作线程安全），事件入队
        server.OnClientConnected = (conn) =>
        {
            int cid = Interlocked.Increment(ref nextClientId);
            conn.ClientId = cid;
            guidToId[conn.ConnectionId] = cid;
            idToGuid[cid] = conn.ConnectionId;
            int captured = cid;
            serverEventQueue.Enqueue(() => OnServerClientConnected?.Invoke(captured));
        };

        server.OnClientDisconnected = (conn) =>
        {
            if (guidToId.TryRemove(conn.ConnectionId, out int cid))
            {
                idToGuid.TryRemove(cid, out _);
                int captured = cid;
                serverEventQueue.Enqueue(() => OnServerClientDisconnected?.Invoke(captured));
            }
        };

        // UDP 发送回调：把数据发回给客户端
        server.OnSendToClient = (endpoint, data) =>
        {
            try { udpListener?.Send(data, data.Length, endpoint); }
            catch { /* 忽略发送异常 */ }
        };

        try
        {
            udpListener = new System.Net.Sockets.UdpClient(port);
            listenerRunning = true;
            listenerThread = new Thread(ListenerLoop)
            {
                IsBackground = true,
                Name = $"LiteNetTransport-{port}"
            };
            listenerThread.Start();
        }
        catch (Exception ex)
        {
            server = null;
            LastErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[LiteNetTransport] 启动服务端失败: {ex.Message}");
            return false;
        }

        return true;
    }

    public void StopServer()
    {
        listenerRunning = false;
        udpListener?.Close();

        // 等待后台线程退出，防止后续访问已释放的资源
        if (listenerThread != null && listenerThread.IsAlive)
        {
            if (!listenerThread.Join(2000))
                System.Diagnostics.Debug.WriteLine("[LiteNetTransport] 监听线程未在 2s 内退出");
        }
        listenerThread = null;

        server = null;
        guidToId.Clear();
        idToGuid.Clear();
    }

    public void SendToClient(int connectionId, byte[] data, bool reliable)
    {
        if (server == null) return;

        if (connectionId == localClientId)
        {
            // Host 模式本地客户端：直接注入收件箱
            client?.InjectLocalData(data);
            return;
        }

        if (idToGuid.TryGetValue(connectionId, out Guid guid))
        {
            if (reliable)
                server.SendTo(guid, data);
            else
                server.SendToUnreliable(guid, data);
        }
    }

    public void Broadcast(byte[] data, bool reliable, int excludeId = -1)
    {
        if (server == null) return;

        Guid excludeGuid = Guid.Empty;
        bool includeLocal = true;

        if (excludeId != -1)
        {
            if (excludeId == localClientId)
            {
                includeLocal = false;
            }
            else if (idToGuid.TryGetValue(excludeId, out Guid eg))
            {
                excludeGuid = eg;
            }
        }

        if (reliable)
            server.Broadcast(data, excludeGuid, includeLocal);
        else
            server.BroadcastUnreliable(data, excludeGuid, includeLocal);
    }

    // ==================== 客户端方法 ====================

    public bool Connect(string address, int port)
    {
        if (client != null)
            throw new InvalidOperationException("客户端已在运行中");

        client = new NetworkClient();
        WireClientEvents();
        client.Connect(address, port);
        return true;
    }

    public bool ConnectAsLocal()
    {
        if (client != null)
            throw new InvalidOperationException("客户端已在运行中");

        if (server == null)
            throw new InvalidOperationException("Host 模式需要先启动服务端");

        isHostMode = true;
        client = new NetworkClient();
        WireClientEvents();

        // 服务端 → 本地客户端：通过 InjectLocalData
        localClientId = server.ConnectLocalClient(data =>
        {
            client.InjectLocalData(data);
        });

        // 本地客户端 → 服务端：直接触发上层事件
        client.OnSendToLocalServer = data =>
        {
            OnServerDataReceived?.Invoke(localClientId, data);
        };

        client.ConnectAsLocal();
        return true;
    }

    public void Disconnect()
    {
        if (isHostMode && server != null)
        {
            server.DisconnectLocalClient();
            localClientId = -1;
        }

        client?.Disconnect();
        client = null;
        isHostMode = false;
    }

    public void SendToServer(byte[] data, bool reliable)
    {
        client?.Send(data, reliable);
    }

    // ==================== 通用 ====================

    public void Update()
    {
        server?.Update();
        client?.Update();

        // 主线程处理：客户端收到数据
        while (clientReceiveQueue.TryDequeue(out byte[]? data))
            OnClientDataReceived?.Invoke(data);

        // 主线程处理：服务端收到数据
        while (serverReceiveQueue.TryDequeue(out var item))
            OnServerDataReceived?.Invoke(item.Item1, item.Item2);

        // 主线程处理：服务端连接事件
        while (serverEventQueue.TryDequeue(out var action))
            action();
    }

    // ==================== 内部方法 ====================

    private void WireClientEvents()
    {
        if (client == null) return;

        client.OnConnected += () => OnClientConnected?.Invoke();
        client.OnDisconnected += () => OnClientDisconnected?.Invoke();
        // 后台线程收到的数据先入队列，由主线程 Update 处理
        client.OnDataReceived += data => clientReceiveQueue.Enqueue(data);
        client.OnLog = msg => System.Console.WriteLine("[NetClient] " + msg);
    }

    // ==================== UDP 监听线程（服务端接收） ====================

    private System.Net.Sockets.UdpClient? udpListener;
    private Thread? listenerThread;
    private volatile bool listenerRunning;

    // 服务端 UDP 接收循环。收到数据后根据远端端点找到对应的 ClientConnection，
    // 把数据喂给该连接绑定的 ReliableChannel。
    private void ListenerLoop()
    {
        while (listenerRunning)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[]? received = udpListener?.Receive(ref remote);
                if (received == null || received.Length == 0) continue;

                if (server == null) continue;

                // 查找或创建连接
                var conn = server.FindConnection(remote);
                if (conn == null)
                {
                    // 新客户端连入
                    conn = server.AddConnection(remote);
                }

                // 更新心跳
                server.UpdateHeartbeat(conn.ConnectionId);

                // 喂给该连接的 ReliableChannel
                conn.Channel.OnRawDataReceived(received);
            }
            catch (ObjectDisposedException)
            {
                System.Console.WriteLine("[ListenerLoop] ObjectDisposedException，线程退出");
                break;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                System.Console.WriteLine($"[ListenerLoop] SocketException({ex.NativeErrorCode})，继续");
                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[ListenerLoop] {ex.GetType().Name}: {ex.Message}，继续");
                Thread.Sleep(10);
            }
        }
    }
}
}
