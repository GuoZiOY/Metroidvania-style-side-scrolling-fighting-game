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
        [SerializeField] private KeyCode sendKey = KeyCode.S;

        private NetworkManager _netManager;
        private int _sendCount;
        private int _receiveCount;
        private string _statusText = "空闲";
        private Vector2 _scrollPos;

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
            if (Input.GetKeyDown(sendKey)) SendTestMessage();
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
            _sendCount = 0;
            _receiveCount = 0;
            Debug.Log("[NetworkTest] 网络已停止");
        }

        private void SendTestMessage()
        {
            if (_netManager == null || !_netManager.IsClient) return;

            _sendCount++;
            string text = $"测试消息 #{_sendCount} 来自 {(_netManager.IsHost ? "Host" : "Client")} Time={Time.time:F2}";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
            _netManager.SendToServer(data, true);
            Debug.Log($"[NetworkTest] 发送: {text}");
        }

        void OnDestroy()
        {
            StopNet();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 300), GUI.skin.box);
            GUILayout.Label("=== 网络联机验证 ===");
            GUILayout.Label($"状态: {_statusText}");
            GUILayout.Label($"发送: {_sendCount} | 收到: {_receiveCount}");
            GUILayout.Space(5);

            if (!_netManager.IsRunning)
            {
                if (GUILayout.Button($"启动 Host (H) 端口 {port}"))
                    StartHost();
                if (GUILayout.Button($"启动 Client (C) 连 127.0.0.1:{port}"))
                    StartClient();
            }
            else
            {
                if (GUILayout.Button($"停止 (X)"))
                    StopNet();
                if (_netManager.IsClient && !_netManager.IsHost)
                {
                    if (GUILayout.Button($"发消息 (S)"))
                        SendTestMessage();
                }
            }

            GUILayout.Label("", GUI.skin.horizontalSlider);

            // 测试说明
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            GUILayout.Label("测试步骤：", EditorBold());
            GUILayout.Label("1. 运行第一个 Unity 实例，点「启动 Host」");
            GUILayout.Label("2. 运行第二个 Unity 实例，点「启动 Client」");
            GUILayout.Label("3. 在客户端按 S 发消息，Host 应收到并回声");
            GUILayout.Label("4. 两个实例的 Console 都会显示收发日志");
            GUILayout.Space(5);
            GUILayout.Label("同一台机器测试：", EditorBold());
            GUILayout.Label("打包一个 Build，Editor 跑 Host，Build 跑 Client");
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private GUIStyle EditorBold()
        {
            var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return style;
        }
    }
}
