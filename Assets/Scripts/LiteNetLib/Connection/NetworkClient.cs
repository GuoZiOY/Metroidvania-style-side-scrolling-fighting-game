#nullable enable
using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LiteNetLib.Reliability;

namespace LiteNetLib.Connection
{

// 客户端连接管理器 —— 与服务端的 ConnectionManager 对应。
// 每个客户端一个实例，维护到服务端的单一 UDP 连接。
public class NetworkClient
{
    private UdpClient? udpClient;
    private IPEndPoint? serverEndPoint;
    private Thread? receiveThread;
    private volatile bool isRunning;

    private readonly ReliableChannel channel = new();
    private readonly ConcurrentQueue<byte[]> incomingQueue = new();

    // ---- 本地环回模式 ----
    private bool isLocalMode;

    // ---- 内部回调（供 LiteNetTransport 绑定） ----
    internal Action<byte[]>? OnSendToLocalServer;

    // ---- 状态 ----
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsLocalMode => isLocalMode;
    public int Port { get; private set; }

    // ---- 事件 ----
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action<byte[]>? OnDataReceived;
    public Action<string>? OnLog;

    public NetworkClient()
    {
        channel.OnReliableDataReceived = data => OnDataReceived?.Invoke(data);
        channel.OnUnreliableDataReceived = data => OnDataReceived?.Invoke(data);
        channel.OnLog = msg => OnLog?.Invoke(msg);
        channel.OnSendFailed = (seq, data) =>
            OnLog?.Invoke($"[NetworkClient] 消息 seq={seq} 发送失败（重试5次超限）");
    }

    // 通过 UDP 连接到服务器
    public void Connect(string host, int port)
    {
        if (State != ConnectionState.Disconnected)
            throw new InvalidOperationException($"无法连接：当前状态为 {State}");

        State = ConnectionState.Connecting;
        Port = port;

        serverEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
        udpClient = new UdpClient();
        udpClient.Connect(serverEndPoint);

        // ReliableChannel 的发送回调 → 实际 UDP 发送
        channel.OnSendRawData = data =>
        {
            try { udpClient?.Send(data, data.Length); }
            catch (Exception ex) { OnLog?.Invoke($"[NetworkClient] 发送异常: {ex.Message}"); }
        };

        // 启动后台接收线程
        isRunning = true;
        receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = $"NetClient-{host}:{port}"
        };
        receiveThread.Start();

        State = ConnectionState.Connected;
        OnLog?.Invoke($"[NetworkClient] 已连接到 {host}:{port}");
        OnConnected?.Invoke();
    }

    // 作为本地客户端连接（Host 模式，不走 UDP）
    public void ConnectAsLocal()
    {
        if (State != ConnectionState.Disconnected)
            throw new InvalidOperationException($"无法连接：当前状态为 {State}");

        isLocalMode = true;
        State = ConnectionState.Connected;
        OnLog?.Invoke("[NetworkClient] 以本地模式连接（Host 环回）");
        OnConnected?.Invoke();
    }

    // 断开连接
    public void Disconnect()
    {
        if (State == ConnectionState.Disconnected) return;

        State = ConnectionState.Disconnecting;
        isRunning = false;

        udpClient?.Close();

        if (receiveThread != null && receiveThread.IsAlive)
        {
            if (!receiveThread.Join(1000))
                OnLog?.Invoke("[NetworkClient] 接收线程未在 1s 内退出");
            receiveThread = null;
        }

        udpClient = null;
        State = ConnectionState.Disconnected;
        OnLog?.Invoke("[NetworkClient] 已断开连接");
        OnDisconnected?.Invoke();
    }

    // 发送数据到服务端
    public void Send(byte[] data, bool reliable)
    {
        if (State != ConnectionState.Connected)
        {
            OnLog?.Invoke("[NetworkClient] 未连接，无法发送");
            return;
        }

        if (isLocalMode)
        {
            // Host 模式：直接入服务端收件箱，不走 UDP
            OnSendToLocalServer?.Invoke(data);
            return;
        }

        if (reliable)
            channel.SendReliable(data);
        else
            channel.SendUnreliable(data);
    }

    private DateTime lastHeartbeat = DateTime.UtcNow;

    // 每帧调用，驱动 ReliableChannel 重传定时器
    public void Update()
    {
        if (isLocalMode)
        {
            // 本地模式：从队列取"收到的"数据
            while (incomingQueue.TryDequeue(out byte[]? data))
                OnDataReceived?.Invoke(data);
            return;
        }

        channel.Update();

        // 客户端心跳：每秒发一个不可靠包，告诉服务端我还活着
        if (State == ConnectionState.Connected && (DateTime.UtcNow - lastHeartbeat).TotalSeconds >= 1)
        {
            lastHeartbeat = DateTime.UtcNow;
            channel.SendUnreliable(new byte[] { 0 }); // 1 字节的心跳
        }
    }

    // ==================== 内部方法 ====================

    // Host 模式下，服务端调用此方法将数据注入客户端的收件箱
    internal void InjectLocalData(byte[] data)
    {
        incomingQueue.Enqueue(data);
    }

    // 接收线程：从 UDP Socket 读取数据并喂给 ReliableChannel
    private void ReceiveLoop()
    {
        OnLog?.Invoke("[NetworkClient] ReceiveLoop: 线程启动");
        while (isRunning)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[]? received = udpClient?.Receive(ref remote);
                if (received != null && received.Length > 0)
                {
                    channel.OnRawDataReceived(received);
                }
            }
            catch (ObjectDisposedException)
            {
                OnLog?.Invoke("[NetworkClient] ReceiveLoop: ObjectDisposedException，线程退出");
                break;
            }
            catch (SocketException ex)
            {
                OnLog?.Invoke($"[NetworkClient] ReceiveLoop: SocketException({ex.NativeErrorCode})，继续");
                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[NetworkClient] ReceiveLoop: {ex.GetType().Name}: {ex.Message}，继续");
                Thread.Sleep(10);
            }
        }

        OnLog?.Invoke("[NetworkClient] 接收线程已退出");
    }
}
}
