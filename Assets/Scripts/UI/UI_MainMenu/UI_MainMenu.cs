using System;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单控制器。
/// 管理标题画面、菜单导航、存档面板的联动。
/// 挂载在 MainMenu 场景的 Canvas 根对象上。
/// </summary>
public class UI_MainMenu : MonoBehaviour
{
    #region 序列化字段

    [Header("主菜单组")]
    [SerializeField] private CanvasGroup menuGroup;          // 主按钮组（新游戏/继续/读档/设置/退出）
    [SerializeField] private TextMeshProUGUI titleText;      // 游戏标题
    [SerializeField] private Button newGameBtn;              // 新游戏
    [SerializeField] private Button continueBtn;             // 继续游戏
    [SerializeField] private Button loadGameBtn;             // 读档
    [SerializeField] private Button settingsBtn;             // 设置
    [SerializeField] private Button quitBtn;                 // 退出

    [Header("存档面板")]
    [SerializeField] private UI_SavePanel savePanel;         // 存档选择面板

    [Header("设置面板（把现有 UI_Setting 的对象拖进来）")]
    [SerializeField] private GameObject settingsPanel;       // 设置面板根对象
    [SerializeField] private Button settingsBackBtn;         // 设置面板返回按钮

    [Header("动画参数")]
    [SerializeField] private float fadeDuration = 0.3f;

    #endregion

    #region Unity 生命周期

    private void Start()
    {
        // 标题进入动画
        if (titleText != null)
        {
            titleText.transform.localScale = Vector3.one * 0.8f;
            titleText.alpha = 0;
            DOTween.Sequence()
                .Join(titleText.transform.DOScale(Vector3.one, 0.6f).SetEase(Ease.OutBack))
                .Join(titleText.DOFade(1, 0.4f));
        }

        // 菜单按钮初始隐藏，然后渐入
        if (menuGroup != null)
        {
            menuGroup.alpha = 0;
            menuGroup.DOFade(1, fadeDuration).SetDelay(0.3f);
        }

        // 绑定按钮事件
        newGameBtn?.onClick.AddListener(OnNewGame);
        continueBtn?.onClick.AddListener(OnContinue);
        loadGameBtn?.onClick.AddListener(OnLoadGame);
        settingsBtn?.onClick.AddListener(OnSettings);
        quitBtn?.onClick.AddListener(OnQuit);

        // 继续按钮：无存档时不可用
        RefreshContinueButton();

        // 设置面板返回
        settingsBackBtn?.onClick.AddListener(HideSettings);

        // 存档面板确认事件
        savePanel.OnSlotConfirmed += OnSlotConfirmed;

        // 默认隐藏设置面板和存档面板
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (savePanel != null)
            savePanel.OnSlotConfirmed -= OnSlotConfirmed;
    }

    #endregion

    #region 菜单按钮

    private void OnNewGame()
    {
        AnimateButtonClick(newGameBtn);
        savePanel.ShowForNewGame();
        HideMenuGroup();
    }

    private void OnContinue()
    {
        AnimateButtonClick(continueBtn);
        // 继续游戏 = 加载自动槽或最近存档
        var profiles = SaveManager.Instance?.ListProfiles();
        if (profiles != null && profiles.Count > 0)
        {
            // 找最近存档的槽位
            SaveProfile latest = null;
            foreach (var p in profiles)
            {
                if (p.isEmpty) continue;
                if (latest == null ||
                    string.Compare(p.saveTime, latest.saveTime, StringComparison.Ordinal) > 0)
                    latest = p;
            }
            if (latest != null)
                SaveManager.Instance?.Load(latest.slotIndex);
        }
    }

    private void OnLoadGame()
    {
        AnimateButtonClick(loadGameBtn);
        savePanel.ShowForLoad();
        HideMenuGroup();
    }

    private void OnSettings()
    {
        AnimateButtonClick(settingsBtn);
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            var cg = settingsPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = settingsPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            cg.DOFade(1, fadeDuration);
        }
        HideMenuGroup();
    }

    private void OnQuit()
    {
        AnimateButtonClick(quitBtn);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region 存档面板回调

    private void OnSlotConfirmed(int slotIndex, UI_SavePanel.PanelMode mode)
    {
        // 存档/读档后，主菜单隐藏（场景会自动切换）
        // 仅新游戏模式需要手动切换场景，已在 SavePanel 中处理
        // 可在此添加额外逻辑
    }

    #endregion

    #region 设置面板

    private void HideSettings()
    {
        if (settingsPanel == null) return;
        var cg = settingsPanel.GetComponent<CanvasGroup>();
        cg?.DOFade(0, fadeDuration).OnComplete(() =>
        {
            settingsPanel.SetActive(false);
            ShowMenuGroup();
        });
    }

    #endregion

    #region 菜单组显隐

    private void ShowMenuGroup()
    {
        if (menuGroup == null) return;
        menuGroup.gameObject.SetActive(true);
        menuGroup.alpha = 0;
        menuGroup.DOFade(1, fadeDuration);
    }

    private void HideMenuGroup()
    {
        if (menuGroup == null) return;
        menuGroup.DOFade(0, fadeDuration).OnComplete(() =>
            menuGroup.gameObject.SetActive(false));
    }

    #endregion

    #region 工具方法

    private void RefreshContinueButton()
    {
        if (continueBtn == null) return;
        bool hasSave = false;
        if (SaveManager.Instance != null)
        {
            var profiles = SaveManager.Instance.ListProfiles();
            hasSave = profiles != null && profiles.Exists(p => !p.isEmpty);
        }
        continueBtn.interactable = hasSave;

        // 让 continueBtn 的文本根据有无存档变化
        var btnText = continueBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
            btnText.SetText(hasSave ? "继续游戏" : "继续游戏");
    }

    private void AnimateButtonClick(Button btn)
    {
        if (btn == null) return;
        btn.transform.DOKill();
        btn.transform.localScale = Vector3.one;
        btn.transform.DOPunchScale(Vector3.one * 0.1f, 0.12f, 1, 0.5f);
    }

    #endregion
}
