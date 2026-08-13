using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 死亡画面 UI。只负责显示面板和打字机，世界效果由 Player 处理。
public class UI_DeathScreen : MonoBehaviour
{
    [Header("UI 组件")]
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private TextMeshProUGUI causeText;
    [SerializeField] private Button continueBtn;
    [SerializeField] private Button mainMenuBtn;

    [Header("参数")]
    [SerializeField] private string playerTitle = "冒险者";

    [Header("打字机效果")]
    [SerializeField] private float typeCharInterval = 0.1f;
    [SerializeField] private float typePunctuationDelay = 0.3f;

    private bool isShowing;
    private Player player;
    private string killerName = "未知";

    private void Start()
    {
        // 注意：这里不再操作 screenGroup（不设 alpha=0、不 SetActive(false)）。
        // 死亡面板开局由 UIManager.CloseAllPanelsAtStart 统一关闭；若本 Start 延迟到
        // Show() 之后才执行（面板激活时才跑），这里的 SetActive(false) 会把 Show() 刚激活
        // 的面板再次关掉，导致死亡面板永远不显示。alpha/激活完全交给 Show() 控制。
        continueBtn?.onClick.AddListener(OnContinue);
        mainMenuBtn?.onClick.AddListener(OnMainMenu);

        Invoke(nameof(FindPlayer), 0.5f);
    }

    private void FindPlayer()
    {
        player = FindAnyObjectByType<Player>();
    }

    /// <summary>由 Player 死亡协程调用</summary>
    public void Show()
    {
        if (isShowing) return;
        isShowing = true;

        Debug.Log($"[UI_DeathScreen] Show: screenGroup={(screenGroup != null)} isShowing=true");

        // 读取死亡原因（击杀者名字）
        // 防御：死亡面板开局 inactive 时 Start 延迟执行，Invoke(FindPlayer) 可能未触发，
        // player 为 null 则当场补查，避免击杀者名永远是"未知"
        if (player == null)
            player = FindAnyObjectByType<Player>();

        if (player != null)
        {
            var health = player.GetComponent<Entity_Health>();
            if (health != null && !string.IsNullOrEmpty(health.lastAttackerName))
                killerName = health.lastAttackerName;
        }

        if (screenGroup == null)
        {
            Debug.LogError("[UI_DeathScreen] screenGroup 为 null！");
            return;
        }

        screenGroup.gameObject.SetActive(true);
        screenGroup.alpha = 0;
        if (causeText != null) causeText.text = "";

        screenGroup.DOKill();
        screenGroup.DOFade(1, 0.4f).SetUpdate(true).OnComplete(() =>
        {
            if (causeText != null)
            {
                string fullText = $"{playerTitle} 死于 <color=#FF4444>{killerName}</color> 之手";
                TypewriterEffect.Play(causeText, fullText, typeCharInterval, typePunctuationDelay);
            }
        });
    }

    private void OnContinue()
    {
        DOTween.Kill(screenGroup);
        DOTween.Kill(causeText);
        Time.timeScale = 1;
        Hide(); // 关闭死亡面板（UI 系统 DontDestroyOnLoad 持久，重载场景不会自动关）

        if (SaveManager.Instance != null && SaveManager.Instance.CurrentSlotIndex >= 0)
            SaveManager.Instance.LoadWithReload(SaveManager.Instance.CurrentSlotIndex);
        else
            Debug.LogError("[UI_DeathScreen] 无当前存档槽位");
    }

    private void OnMainMenu()
    {
        DOTween.Kill(screenGroup);
        DOTween.Kill(causeText);
        Time.timeScale = 1;
        Hide(); // 回主菜单也关掉死亡面板（持久 UI）
        SceneTransitionFader.Instance.TransitionToScene("主菜单"); // 过场黑幕过渡
    }

    // 关闭死亡面板（持久 UI 下场景重载不会销毁它，必须主动隐藏；重置 isShowing 允许再次死亡时弹出）
    private void Hide()
    {
        isShowing = false;
        if (screenGroup != null)
        {
            screenGroup.DOKill();
            screenGroup.gameObject.SetActive(false);
        }
    }
}
