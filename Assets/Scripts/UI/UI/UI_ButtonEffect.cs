using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 按钮悬停放大 + 点击弹回效果。
// 支持动态 restScale：PanelSwitcher 切换选中状态时调整基准缩放。
// 在 UIManager 初始化时自动扫描场景中所有按钮添加效果，无需手动挂载。
public class UI_ButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("交互缩放")]
    [SerializeField] private float hoverMultiplier = 1.08f; // hover 时相对 restScale 的倍数
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float clickPunch = 0.15f;
    [SerializeField] private float clickDuration = 0.1f;
    [SerializeField] private float selectedMultiplier = 1.05f; // 选中时保持放大的倍数

    [HideInInspector] public float restScale; // 基准缩放（Awake 初始化，PanelSwitcher 动态修改）
    [HideInInspector] public bool isSelected; // 是否处于选中放大态（hover 在其基础上叠加，避免覆盖选中）

    private Button _btn;
    private Tween _scaleTween;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        restScale = transform.localScale.x; // 以实际摆放缩放为准
        _btn.onClick.AddListener(PlayClickEffect);
    }

    // 当前目标缩放 = 基准 × (选中放大?) × (hover 放大?)
    private float CurrentTargetScale(bool hover)
    {
        float s = restScale * (isSelected ? selectedMultiplier : 1f);
        return hover ? s * hoverMultiplier : s;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_btn.interactable) return;
        _scaleTween?.Kill();
        // SetUpdate 忽略 timeScale：商店/工作台暂停（timeScale=0）时按钮动画也正常
        // hover 在选中放大基础上叠加，鼠标移开回到选中态（不丢失选中放大）
        _scaleTween = transform.DOScale(CurrentTargetScale(true), hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(CurrentTargetScale(false), hoverDuration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    // 选中态：保持放大（列表项/配方行点击选中用），取消恢复。hover 会在选中基础上叠加。
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        _scaleTween?.Kill();
        transform.DOKill();
        _scaleTween = transform.DOScale(CurrentTargetScale(false), 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    /// <summary>立刻回到基准缩放（切换面板时用）</summary>
    public void SnapToRestScale()
    {
        _scaleTween?.Kill();
        transform.localScale = Vector3.one * restScale;
    }

    // 平滑过渡到基准缩放（PanelSwitcher 切换选中态时调用，SetUpdate 忽略 timeScale）
    public void AnimateToRestScale(float duration = 0.2f)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(restScale, duration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void PlayClickEffect()
    {
        transform.DOKill();
        transform.localScale = Vector3.one * restScale;
        transform.DOPunchScale(Vector3.one * clickPunch, clickDuration, 1, 0.5f).SetUpdate(true);
    }

    private void OnDestroy() { _scaleTween?.Kill(); }

    // ==================== 全局挂接 ====================

    public static void HookAll(bool includeInactive = true)
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            if (btn.GetComponent<UI_ButtonEffect>() != null) continue;
            if (btn.gameObject.scene.name == null) continue;
            btn.gameObject.AddComponent<UI_ButtonEffect>();
        }
        //Debug.Log($"[UI_ButtonEffect] 已为 {buttons.Length} 个按钮添加效果");
    }
}
