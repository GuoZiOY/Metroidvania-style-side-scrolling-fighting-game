using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Networking
{
    // 聊天 UI 面板。每条消息是一个独立的 TextMeshPro 对象。
    // Content 自动用 VerticalLayoutGroup 排列 + ContentSizeFitter 扩展。
    public class UI_Chat : MonoBehaviour
    {
        [Header("UI 组件（手动拖拽）")]
        public GameObject panel;                    // 聊天面板根对象
        public TMP_InputField inputField;           // 输入框
        public RectTransform messageContainer;      // Content（挂 VerticalLayoutGroup + ContentSizeFitter）
        public ScrollRect scrollRect;               // 滚动区域
        public ChatManager chatManager;             // 聊天管理器

        [Header("按键设置")]
        public KeyCode toggleKey = KeyCode.Y;
        public KeyCode sendKey = KeyCode.Return;

        [Header("玩家颜色")]
        public string[] playerColors = new string[]
        {
            "#88CCFF", "#FF6B6B", "#51CF66", "#FFD43B", "#DA77F2", "#FF922B"
        };

        [Header("预制体")]
        public GameObject messagePrefab;            // 消息文本预制体（Assets/Prefab/消息文本.prefab）

        [Header("设置")]
        [Tooltip("最多保留的消息条数，超出后自动删除最旧的")]
        public int maxMessages = 50;

        public static bool IsChatFocused { get; private set; }

        private readonly Queue<GameObject> messageQueue = new();
        private bool _isOpen;
        private bool chatInitialized; // 惰性初始化标记

        void Awake()
        {
            EnsureInitialized();
        }

        void Start()
        {
            EnsureInitialized(); // 兜底：active 场景也保证初始化（幂等）
        }

        // 惰性初始化：面板初始 inactive 时 Awake 不执行，首次激活/显隐前保证订阅与布局完成
        private void EnsureInitialized()
        {
            if (chatInitialized)
                return;

            if (chatManager == null)
            {
                Debug.LogError("[UI_Chat] chatManager 未赋值");
                return; // 未接线不置初始化标记，后续可重试
            }
            chatInitialized = true;

            chatManager.OnChatReceived += AddMessage;

            if (inputField != null)
                inputField.onSubmit.AddListener(_ => SendMessage());

            // 确保 messageContainer 有必要的布局组件
            if (messageContainer != null)
            {
                if (messageContainer.GetComponent<VerticalLayoutGroup>() == null)
                {
                    var vlg = messageContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.spacing = 2;
                }
                if (messageContainer.GetComponent<ContentSizeFitter>() == null)
                {
                    var csf = messageContainer.gameObject.AddComponent<ContentSizeFitter>();
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                // 锚点顶部，向下增长
                messageContainer.anchorMin = new Vector2(0, 1);
                messageContainer.anchorMax = new Vector2(1, 1);
                messageContainer.pivot = new Vector2(0.5f, 1);
                messageContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);
            }

            // 自动补全 scrollRect.viewport
            if (scrollRect != null && scrollRect.viewport == null && messageContainer != null)
                scrollRect.viewport = messageContainer.parent as RectTransform;

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

            // Escape 由 UIManager 统一处理（IsChatFocused 优先关闭聊天）
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
                StartCoroutine(ScrollToBottomNextFrame());
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

        private string GetPlayerColorHex(string sender)
        {
            if (sender.StartsWith("玩家") && sender.Length > 2 &&
                int.TryParse(sender.Substring(2), out int idx) &&
                idx >= 1 && idx <= playerColors.Length)
                return playerColors[idx - 1];
            return playerColors[0];
        }

        private void AddMessage(string sender, string content)
        {
            if (messageContainer == null) return;

            // 用预制体生成消息对象
            var msgObj = Instantiate(messagePrefab, messageContainer, false);

            // 设置文本内容
            var tmp = msgObj.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = $"<color={GetPlayerColorHex(sender)}>{sender}</color>: {content}";

            messageQueue.Enqueue(msgObj);

            // 超上限则删除最旧的
            while (messageQueue.Count > maxMessages)
                Destroy(messageQueue.Dequeue());

            StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null; // 等 VerticalLayoutGroup 完成布局

            if (scrollRect == null) yield break;

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0;
        }
    }
}
