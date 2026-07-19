using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Networking
{
    // 聊天 UI 面板。
    // 需要手动在场景中搭建 UI 组件并拖拽引用。
    public class UI_Chat : MonoBehaviour
    {
        [Header("UI 组件（手动拖拽）")]
        public GameObject panel;                // 聊天面板根对象
        public TMP_InputField inputField;       // 输入框
        public TextMeshProUGUI textDisplay;     // 消息显示文本
        public ScrollRect scrollRect;           // 滚动区域
        public ChatManager chatManager;         // 聊天管理器（拖拽场景中的 NetworkTester）

        [Header("按键设置")]
        public KeyCode toggleKey = KeyCode.Y;
        public KeyCode sendKey = KeyCode.Return;

        [Header("玩家颜色")]
        [Tooltip("玩家1~6的颜色，按玩家编号顺序取")]
        public string[] playerColors = new string[]
        {
            "#88CCFF",  // 玩家1 蓝
            "#FF6B6B",  // 玩家2 红
            "#51CF66",  // 玩家3 绿
            "#FFD43B",  // 玩家4 黄
            "#DA77F2",  // 玩家5 紫
            "#FF922B",  // 玩家6 橙
        };

        [Header("设置")]
        public int maxMessages = 50;

        // 聊天面板是否正在输入（供游戏逻辑判断，聊天打开时屏蔽游戏快捷键）
        public static bool IsChatFocused { get; private set; }

    private readonly List<string> _messages = new();
        private bool _isOpen;

        void Start()
        {
            if (chatManager == null)
            {
                Debug.LogError("[UI_Chat] chatManager 未赋值，请在 Inspector 中拖拽");
                return;
            }

            chatManager.OnChatReceived += AddMessage;

            // 按回车发送（InputField 会吞掉 Update 里的回车检测，所以这里也监听）
            if (inputField != null)
                inputField.onSubmit.AddListener(_ => SendMessage());

            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (panel == null) return;

            if (Input.GetKeyDown(toggleKey))
                ShowChat(!_isOpen);

            if (!_isOpen) return;

            if (Input.GetKeyDown(sendKey) && !Input.imeIsSelected)
                SendMessage();

            if (Input.GetKeyDown(KeyCode.Escape))
                ShowChat(false);
        }

        void OnDestroy()
        {
            if (chatManager != null)
                chatManager.OnChatReceived -= AddMessage;
        }

        public void ShowChat(bool show)
        {
            if (panel == null) return;
            _isOpen = show;
            IsChatFocused = show;
            panel.SetActive(show);

            if (show)
            {
                if (inputField != null)
                {
                    inputField.Select();
                    inputField.ActivateInputField();
                }
                ScrollToBottom();
            }
        }

        public void SendMessage()
        {
            if (inputField == null || string.IsNullOrEmpty(inputField.text)) return;

            chatManager?.Send(inputField.text.Trim());
            inputField.text = "";
            inputField.Select();
            inputField.ActivateInputField();
        }

        private string GetPlayerColor(string sender)
        {
            // 从"玩家N"中提取编号，取对应颜色
            if (sender.StartsWith("玩家") && sender.Length > 2)
            {
                if (int.TryParse(sender.Substring(2), out int idx) &&
                    idx >= 1 && idx <= playerColors.Length)
                {
                    return playerColors[idx - 1];
                }
            }
            return playerColors[0]; // 默认第一个颜色
        }

        private void AddMessage(string sender, string content)
        {
            Debug.Log($"[UI_Chat] AddMessage: {sender}: {content}");
            string color = GetPlayerColor(sender);
            string line = $"<color={color}>{sender}</color>: {content}";
            _messages.Add(line);

            if (_messages.Count > maxMessages)
                _messages.RemoveAt(0);

            if (textDisplay != null)
            {
                textDisplay.text = string.Join("\n", _messages);
            }
            else
            {
                Debug.LogWarning("[UI_Chat] textDisplay 未赋值，无法显示消息");
            }

            if (_isOpen)
                ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0;
            }
        }
    }
}
