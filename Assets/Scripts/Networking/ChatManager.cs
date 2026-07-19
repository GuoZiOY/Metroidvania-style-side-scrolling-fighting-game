using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LiteNetLib.Dispatch;

namespace Networking
{
    // 聊天管理器。负责聊天消息的网络收发。
    // 服务端自动为每个连接分配玩家编号（玩家1、玩家2...），
    // 客户端只管发文字，名字和颜色由服务端确定。
    // 需在 NetworkManager 启动后调用 Init() 注册事件。
    public class ChatManager : MonoBehaviour
    {
        private const int MSG_CHAT = 200;

        public event Action<string, string> OnChatReceived;
        public event Action OnNewMessage;

        private NetworkManager Net => NetworkManager.Instance;
        private bool _registered;

        // 服务端：玩家编号分配（后台线程可能调用，加锁保护）
        private readonly Dictionary<int, int> clientToPlayer = new();
        private readonly object playerLock = new();
        private int nextPlayerIndex = 1;

        void OnDisable() => UnregisterHandlers();
        void OnDestroy() => UnregisterHandlers();

        // 网络启动后由外部调用，确保 Transport 已存在
        public void Init()
        {
            if (_registered) return;
            if (Net == null || Net.Transport == null) return;
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            if (_registered) return;
            if (Net == null || Net.Transport == null) return;

            if (Net.IsServer)
            {
                Net.Transport.OnServerClientConnected += OnClientConnected;
                Net.Transport.OnServerDataReceived += OnServerData;
            }
            if (Net.IsClient)
                Net.Transport.OnClientDataReceived += OnClientData;

            _registered = true;
            Debug.Log("[ChatManager] 事件注册完成");
        }

        private void UnregisterHandlers()
        {
            if (!_registered) return;
            if (Net?.Transport != null)
            {
                if (Net.IsServer)
                {
                    Net.Transport.OnServerClientConnected -= OnClientConnected;
                    Net.Transport.OnServerDataReceived -= OnServerData;
                }
                if (Net.IsClient)
                    Net.Transport.OnClientDataReceived -= OnClientData;
            }
            _registered = false;
        }

        // 服务端：新客户端连接 → 分配玩家编号
        private void OnClientConnected(int clientId)
        {
            lock (playerLock)
            {
                if (!clientToPlayer.ContainsKey(clientId))
                    clientToPlayer[clientId] = nextPlayerIndex++;
            }
        }

        // 服务端：收到聊天消息 → 广播给其他客户端
        private void OnServerData(int clientId, byte[] data)
        {
            if (data == null || data.Length < 4) return;

            var (msgId, body) = MessageDispatcher.UnpackMessage(data);
            if (msgId != MSG_CHAT) return;

            // 如果还没分配编号（比如 Host 的本地客户端），现在分配
            int playerIdx;
            lock (playerLock)
            {
                if (!clientToPlayer.ContainsKey(clientId))
                    clientToPlayer[clientId] = nextPlayerIndex++;
                playerIdx = clientToPlayer[clientId];
            }
            string messageContent = Encoding.UTF8.GetString(body);

            // 打包：服务端写入玩家编号后广播给其他客户端
            string full = $"玩家{playerIdx}\0{messageContent}";
            byte[] newBody = Encoding.UTF8.GetBytes(full);
            byte[] newPacket = MessageDispatcher.PackMessage(MSG_CHAT, newBody);

            // 广播给所有人（包括发送者），消息经过服务端格式化后才显示
            Net.Broadcast(newPacket, true);
        }

        // 客户端：收到聊天消息 → 拆包触发事件
        private void OnClientData(byte[] data)
        {
            if (data == null || data.Length < 4) return;

            var (msgId, body) = MessageDispatcher.UnpackMessage(data);
            if (msgId != MSG_CHAT) return;

            string full = Encoding.UTF8.GetString(body);
            int sep = full.IndexOf('\0');
            if (sep < 0) return;

            string sender = full.Substring(0, sep);
            string content = full.Substring(sep + 1);

            OnChatReceived?.Invoke(sender, content);
            OnNewMessage?.Invoke();
        }

        // 发送聊天消息（由 UI 调用）。客户端只管发文字，名字由服务端填。
        public void Send(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (Net == null || !Net.IsClient) return;

            byte[] body = Encoding.UTF8.GetBytes(message);
            byte[] packet = MessageDispatcher.PackMessage(MSG_CHAT, body);

            Net.SendToServer(packet, true);
        }
    }
}
