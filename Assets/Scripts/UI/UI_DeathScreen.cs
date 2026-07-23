using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 死亡画面 UI。只负责显示面板和打字机，世界效果由 Player 处理。
/// </summary>
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

    public static UI_DeathScreen Instance { get; private set; }

    private bool _isShowing;
    private Player _player;
    private string _killerName = "未知";

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (screenGroup != null)
        {
            screenGroup.alpha = 0;
            screenGroup.gameObject.SetActive(false);
        }

        continueBtn?.onClick.AddListener(OnContinue);
        mainMenuBtn?.onClick.AddListener(OnMainMenu);

        Invoke(nameof(FindPlayer), 0.5f);
    }

    private void FindPlayer()
    {
        _player = FindAnyObjectByType<Player>();
    }

    /// <summary>由 Player 死亡协程调用</summary>
    public void Show()
    {
        if (_isShowing) return;
        _isShowing = true;

        // 读取死亡原因
        if (_player != null)
        {
            var health = _player.GetComponent<Entity_Health>();
            if (health != null && !string.IsNullOrEmpty(health.lastAttackerName))
                _killerName = health.lastAttackerName;
        }

        screenGroup.gameObject.SetActive(true);
        screenGroup.alpha = 0;
        if (causeText != null) causeText.text = "";

        screenGroup.DOKill();
        screenGroup.DOFade(1, 0.4f).SetUpdate(true).OnComplete(() =>
        {
            if (causeText != null)
            {
                string fullText = $"{playerTitle} 死于 <color=#FF4444>{_killerName}</color> 之手";
                TypewriterEffect.Play(causeText, fullText, typeCharInterval, typePunctuationDelay);
            }
        });
    }

    private void OnContinue()
    {
        DOTween.Kill(screenGroup);
        DOTween.Kill(causeText);
        Time.timeScale = 1;

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
        SceneManager.LoadScene("主菜单");
    }
}
