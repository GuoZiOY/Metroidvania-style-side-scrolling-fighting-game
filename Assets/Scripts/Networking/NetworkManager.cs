using UnityEngine;
using LiteNetLib;

namespace Networking
{
    // 网络管理器 —— Unity 侧入口。
    // 挂载在场景中，管理网络生命周期。
    // 单机模式下此组件不应存在（或处于非激活状态）。
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("网络配置")]
        [SerializeField] private int defaultPort = 12345;
        [SerializeField] private string defaultAddress = "127.0.0.1";

        private IGameTransport _transport;

        // ---- 状态 ----
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsHost => IsServer && IsClient;
        public bool IsRunning => IsServer || IsClient;

        // ---- 当前传输层 ----
        public IGameTransport Transport => _transport;

        // ---- 本地玩家 NetId（由 SpawnSystem 分配后设置） ----
        public uint LocalPlayerNetId { get; set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NetworkManager] 检测到重复实例，销毁自身");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Update()
        {
            _transport?.Update();
        }

        void OnDestroy()
        {
            Stop();
        }

        void OnApplicationQuit()
        {
            Stop();
        }

        // 启动 Host 模式（服务端 + 本地客户端环回）
        public void StartHost(int port = -1)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[NetworkManager] 已在运行中，无法启动 Host");
                return;
            }

            if (port <= 0) port = defaultPort;
            _transport = CreateTransport();

            if (!_transport.StartServer(port))
            {
                string detail = (_transport as LiteNetTransport)?.LastErrorMessage ?? "未知错误";
                Debug.LogError($"[NetworkManager] 服务端启动失败（端口 {port}）: {detail}");
                _transport = null;
                return;
            }

            IsServer = true;

            if (!_transport.ConnectAsLocal())
            {
                Debug.LogError("[NetworkManager] 本地客户端连接失败");
                Stop();
                return;
            }

            IsClient = true;
            Debug.Log($"[NetworkManager] Host 模式已启动（端口 {port}）");
        }

        // 启动纯客户端，连接到指定地址
        public void StartClient(string address = null, int port = -1)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[NetworkManager] 已在运行中，无法启动客户端");
                return;
            }

            if (string.IsNullOrEmpty(address)) address = defaultAddress;
            if (port <= 0) port = defaultPort;

            _transport = CreateTransport();

            if (!_transport.Connect(address, port))
            {
                Debug.LogError($"[NetworkManager] 连接失败 {address}:{port}");
                _transport = null;
                return;
            }

            IsClient = true;
            Debug.Log($"[NetworkManager] 客户端已连接 {address}:{port}");
        }

        // 停止所有网络活动
        public void Stop()
        {
            if (_transport == null) return;

            _transport.Disconnect();
            _transport.StopServer();
            _transport = null;
            IsServer = false;
            IsClient = false;
            Debug.Log("[NetworkManager] 网络已停止");
        }

        // 创建传输层实例（后续可扩展为根据配置切换 LiteNetTransport / SteamTransport）
        private static IGameTransport CreateTransport()
        {
            return new LiteNetTransport();
        }

        // ---- 便捷发送方法 ----

        // 发送数据到服务端（仅客户端调用）
        public void SendToServer(byte[] data, bool reliable = true)
        {
            _transport?.SendToServer(data, reliable);
        }

        // 发送数据到指定客户端（仅服务端调用）
        public void SendToClient(int connectionId, byte[] data, bool reliable = true)
        {
            _transport?.SendToClient(connectionId, data, reliable);
        }

        // 广播给所有客户端（仅服务端调用）
        public void Broadcast(byte[] data, bool reliable = true, int excludeId = -1)
        {
            _transport?.Broadcast(data, reliable, excludeId);
        }
    }
}
