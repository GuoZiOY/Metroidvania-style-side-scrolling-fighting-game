using UnityEngine;
using LiteNetLib;

namespace Networking
{
    // 网络联机验证脚本。
    // 挂载到场景任意 GameObject 上，Play 后自动启动 Host。
    // 测试内容：Host 模式启动、客户端连接、原始字节收发。
    public class NetworkTest : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private bool autoStartHost = true;
        [SerializeField] private string connectAddress = "127.0.0.1";
        [SerializeField] private int port = 12345;
        [SerializeField] private KeyCode hostKey = KeyCode.H;
        [SerializeField] private KeyCode clientKey = KeyCode.C;
        [SerializeField] private KeyCode stopKey = KeyCode.X;

        private NetworkManager _netManager;
        private string _statusText = "空闲";

        void Start()
        {
            // 联机模式必须后台运行，否则失去焦点时网络会断开
            Application.runInBackground = true;

            // 确保场景中有 NetworkManager
            _netManager = FindAnyObjectByType<NetworkManager>();
            if (_netManager == null)
            {
                GameObject go = new GameObject("NetworkManager");
                _netManager = go.AddComponent<NetworkManager>();
            }

            // 自动添加 ChatManager
            if (FindAnyObjectByType<ChatManager>() == null)
            {
                gameObject.AddComponent<ChatManager>();
            }

            if (autoStartHost)
            {
                StartHost();
            }

            // 网络启动后初始化 ChatManager
            FindAnyObjectByType<ChatManager>()?.Init();
        }

        void Update()
        {
            if (Input.GetKeyDown(hostKey)) StartHost();
            if (Input.GetKeyDown(clientKey)) StartClient();
            if (Input.GetKeyDown(stopKey)) StopNet();
        }

        public void StartHost()
        {
            if (_netManager == null || _netManager.IsRunning) return;

            _netManager.StartHost(port);
            _statusText = $"Host (端口 {port})";
            FindAnyObjectByType<ChatManager>()?.Init();
            Debug.Log($"[NetworkTest] Host 已启动，按 C 开客户端测试");
        }

        public void StartClient()
        {
            if (_netManager == null || _netManager.IsRunning) return;

            _netManager.StartClient(connectAddress, port);
            _statusText = $"客户端 (正在连接 {connectAddress}:{port}...)";
            FindAnyObjectByType<ChatManager>()?.Init();

            _netManager.Transport.OnClientConnected += () =>
            {
                _statusText = $"客户端 (已连接 {connectAddress}:{port})";
                Debug.Log("[NetworkTest] 客户端已连接！");
            };
            _netManager.Transport.OnClientDisconnected += () =>
            {
                _statusText = "客户端 (已断开)";
                Debug.Log("[NetworkTest] 客户端已断开");
            };

            Debug.Log($"[NetworkTest] 正在连接 {connectAddress}:{port}...");
        }

        public void StopNet()
        {
            if (_netManager == null) return;
            _netManager.Stop();
            _statusText = "已停止";
            Debug.Log("[NetworkTest] 网络已停止");
        }

        void OnDestroy()
        {
            StopNet();
        }

    }
}
