using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class PanelEntry
{
    public GameObject panel;
    public Button button;
}

/// <summary>
/// 通用面板切换器。管理 panel ↔ button 映射、选中高亮、切换动画、同按钮再按关闭。
/// 主面板和子面板共用此组件，UIManager 通过事件监听做额外逻辑。
/// </summary>
public class PanelSwitcher : MonoBehaviour
{
    [SerializeField] private PanelEntry[] entries;

    [Header("初始设置")]
    [SerializeField] private bool resetOnEnable = false;     // 每次启用时复位到第一个面板

    [Header("按钮颜色")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color normalColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("按钮缩放")]
    [SerializeField] private float selectedScale = 1.15f;       // 选中时放大
    [SerializeField] private float normalScale = 0.9f;           // 未选中时缩小

    [Header("切换动画")]
    [SerializeField] private PanelAnimation animationType = PanelAnimation.ScaleFade;

    public enum PanelAnimation { None, ScaleFade, SlideFromRight }

    public int CurrentIndex { get; private set; } = -1; // 当前显示索引，-1=全部关闭

    // 外部事件（UIManager 等监听）
    public event Action<int> OnPanelShown;
    public event Action OnAllHidden;

    private Vector2[] originalPositions;
    private Vector3[] originalScales;
    private Vector3[] buttonOriginalScales;  // 按钮原始缩放
    private CanvasGroup[] canvasGroups;
    private bool initialized;

    private void Awake()
    {
        InitializeEntries();
    }

    private void OnEnable()
    {
        if (resetOnEnable && entries.Length > 0)
            ShowPanel(0);
    }

    /// <summary>初始化条目（可从 Awake 或首次使用时调用，支持 GameObject 初始 inactive 的场景）</summary>
    private void InitializeEntries()
    {
        if (initialized) return;
        initialized = true;

        int count = entries.Length;
        originalPositions = new Vector2[count];
        originalScales = new Vector3[count];
        buttonOriginalScales = new Vector3[count];
        canvasGroups = new CanvasGroup[count];

        for (int i = 0; i < count; i++)
        {
            var panel = entries[i].panel;
            if (panel == null) continue;

            // 记录原始位置和缩放
            var rt = panel.GetComponent<RectTransform>();
            if (rt != null) originalPositions[i] = rt.anchoredPosition;
            originalScales[i] = panel.transform.localScale;

            // 记录按钮原始缩放
            var btn = entries[i].button;
            if (btn != null)
                buttonOriginalScales[i] = btn.transform.localScale;

            // 确保有 CanvasGroup
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            canvasGroups[i] = cg;

            // 绑定按钮点击
            int index = i;
            btn?.onClick.AddListener(() => HandleButtonClick(index));
        }
    }

    /// <summary>按钮点击处理</summary>
    private void HandleButtonClick(int index)
    {
        ShowPanel(index);
    }

    /// <summary>显示指定面板（带动画）</summary>
    public void ShowPanel(int index)
    {
        ShowPanelInternal(index, true);
    }

    private void ShowPanelInternal(int index, bool animate)
    {
        if (index < 0 || index >= entries.Length) return;

        // GameObject 初始 inactive 时 Awake 未执行，需要延迟初始化
        InitializeEntries();
        if (entries[index].panel == null) return;

        // 隐藏全部
        HideAllPanels();

        // 显示目标
        var target = entries[index].panel;
        target.SetActive(true);
        if (animate)
            PlayAnimation(index);

        CurrentIndex = index;
        UpdateButtonStates();
        OnPanelShown?.Invoke(index);
    }

    /// <summary>关闭所有面板</summary>
    public void HideAll()
    {
        HideAllPanels();
        CurrentIndex = -1;
        UpdateButtonStates();
        OnAllHidden?.Invoke();
    }

    /// <summary>获取面板索引（按 panel GameObject 查找）</summary>
    public int GetPanelIndex(GameObject panel)
    {
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].panel == panel) return i;
        return -1;
    }

    private void HideAllPanels()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].panel != null)
                entries[i].panel.SetActive(false);
        }
    }

    private void PlayAnimation(int index)
    {
        var panel = entries[index].panel;
        if (panel == null) return;

        // 延迟初始化兜底（首次使用时 Awake 可能未执行）
        InitializeEntries();

        var rt = panel.GetComponent<RectTransform>();
        var cg = canvasGroups != null ? canvasGroups[index] : panel.GetComponent<CanvasGroup>();
        if (rt == null || cg == null) return;

        // 复位到原始位置/缩放
        rt.anchoredPosition = originalPositions[index];
        panel.transform.localScale = originalScales[index];
        cg.alpha = 1f;

        switch (animationType)
        {
            case PanelAnimation.ScaleFade:
                // 动画加 SetUpdate(true)：面板在暂停（timeScale=0，如铁匠/商店工作台）时也能正常淡入
                cg.alpha = 0;
                panel.transform.localScale = originalScales[index] * 0.85f;
                var seq = DOTween.Sequence();
                seq.SetUpdate(true);
                seq.Join(cg.DOFade(1, 0.2f).SetUpdate(true));
                seq.Join(panel.transform.DOScale(originalScales[index], 0.3f).SetEase(Ease.OutBack, 1.3f).SetUpdate(true));
                break;

            case PanelAnimation.SlideFromRight:
                cg.alpha = 0;
                float slideDist = Mathf.Max(rt.rect.width * 0.3f, 50f);
                var seq2 = DOTween.Sequence();
                seq2.SetUpdate(true);
                seq2.Join(cg.DOFade(1, 0.15f).SetUpdate(true));
                seq2.Join(rt.DOAnchorPosX(originalPositions[index].x + slideDist, 0.25f)
                    .SetEase(Ease.OutCubic).From().SetUpdate(true));
                break;

            case PanelAnimation.None:
                cg.alpha = 1;
                panel.transform.localScale = originalScales[index];
                break;
        }
    }

    private void UpdateButtonStates()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            var btn = entries[i].button;
            if (btn == null) continue;

            // 颜色
            bool isSelected = i == CurrentIndex;
            SetButtonColor(btn, isSelected ? selectedColor : normalColor);

            // 缩放：基于按钮原始缩放 × 倍率
            float baseScale = buttonOriginalScales != null && i < buttonOriginalScales.Length
                ? buttonOriginalScales[i].x
                : 1f;
            if (baseScale <= 0) baseScale = 1f;
            float multiplier = isSelected ? selectedScale : normalScale;
            Vector3 targetScale = Vector3.one * (baseScale * multiplier);
            var effect = btn.GetComponent<UI_ButtonEffect>();
            if (effect != null)
            {
                effect.restScale = targetScale.x;
                effect.AnimateToRestScale();
            }
            else
            {
                btn.transform.DOKill();
                // SetUpdate 忽略 timeScale：商店/工作台暂停时按钮选中放大也正常恢复
                btn.transform.DOScale(targetScale, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }
    }

    /// <summary>设置按钮颜色，保留 hover/press 的层次变化，同时同步文本颜色</summary>
    public static void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color * 1.15f;
        cb.pressedColor = color * 0.7f;
        cb.selectedColor = color;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        // 同步子节点文本颜色
        var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null) tmp.color = color;
    }
}
