using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 死亡画面。玩家死亡时淡入，显示死亡原因，可选择读档或返回主菜单。
/// </summary>
public class UI_DeathScreen : MonoBehaviour
{
    [Header("UI 组件")]
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField] private TextMeshProUGUI causeText;     // "冒险者 死于 骷髅 之手"
    [SerializeField] private Button continueBtn;
    [SerializeField] private Button mainMenuBtn;

    [Header("参数")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private string playerTitle = "冒险者";

    private bool _isShowing;
    private Player _player;

    private void Start()
    {
        if (screenGroup != null)
        {
            screenGroup.alpha = 0;
            screenGroup.gameObject.SetActive(false);
        }

        continueBtn?.onClick.AddListener(OnContinue);
        mainMenuBtn?.onClick.AddListener(OnMainMenu);

        Invoke(nameof(SubscribeToPlayer), 0.5f);
    }

    private void SubscribeToPlayer()
    {
        _player = FindAnyObjectByType<Player>();
        if (_player != null)
            _player.OnEntityDead += Show;
        else
            Debug.LogWarning("[UI_DeathScreen] 场景中无 Player");
    }

    public void Show()
    {
        if (_isShowing) return;
        _isShowing = true;

        // 获取死亡原因
        string killerName = "未知";
        if (_player != null)
        {
            var health = _player.GetComponent<Entity_Health>();
            if (health != null && !string.IsNullOrEmpty(health.lastAttackerName))
                killerName = health.lastAttackerName;
        }

        string fullText = $"{playerTitle} 死于 <color=#FF4444>{killerName}</color> 之手";

        screenGroup.gameObject.SetActive(true);
        screenGroup.alpha = 0;
        if (causeText != null) causeText.text = "";

        Time.timeScale = 0;

        // 整体淡入 → 原因文本淡入 + 颜色脉冲
        screenGroup.DOKill();
        screenGroup.DOFade(1, fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            if (causeText != null)
            {
                causeText.text = fullText;
                causeText.alpha = 0;
                causeText.DOFade(1, 0.6f).SetUpdate(true).OnComplete(() =>
                {
                    causeText.DOColor(new Color(1f, 0.6f, 0.2f), 0.6f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetUpdate(true);
                });
            }
        });
    }

    private void OnContinue()
    {
        KillTweens();
        Time.timeScale = 1;

        if (SaveManager.Instance != null && SaveManager.Instance.CurrentSlotIndex >= 0)
            SaveManager.Instance.Load(SaveManager.Instance.CurrentSlotIndex);
        else
            Debug.LogError("[UI_DeathScreen] 无当前存档槽位");
    }

    private void OnMainMenu()
    {
        KillTweens();
        Time.timeScale = 1;
        SceneManager.LoadScene("主菜单");
    }

    private void KillTweens()
    {
        DOTween.Kill(screenGroup);
        DOTween.Kill(causeText);
    }

    private void OnDestroy()
    {
        if (_player != null)
            _player.OnEntityDead -= Show;
    }
}
