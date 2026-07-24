using DG.Tweening;
using UnityEngine;

public class PickupFX : MonoBehaviour
{
    [Header("拖入左上角货币/头像图标的 RectTransform")]
    [SerializeField] private RectTransform uiTarget;

    [Header("拾取动画参数")]
    [SerializeField] private float flyDuration = 0.35f;
    [SerializeField] private Ease flyEase = Ease.InQuad;
    [SerializeField] private float shrinkTo = 0f;

    public static PickupFX Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // UI 目标实时对应的世界坐标（每帧刷新）
    private Vector3 WorldPosition
    {
        get
        {
            if (uiTarget == null) return Vector3.zero;
            Vector3 screenPos = uiTarget.position;
            var cam = Camera.main;
            if (cam != null)
            {
                screenPos.z = Mathf.Abs(cam.transform.position.z);
                return cam.ScreenToWorldPoint(screenPos);
            }
            return screenPos;
        }
    }

    // 供 Gold / ItemAbout 调用，动画期间实时追踪 UI 位置
    public void AnimatePickup(Transform item)
    {
        if (WorldPosition == Vector3.zero) { Destroy(item.gameObject); return; }

        Vector3 startPos = item.position;
        Vector3 startScale = item.localScale;
        float t = 0f;

        DOTween.To(() => t, v => t = v, 1f, flyDuration)
            .SetEase(flyEase)
            .OnUpdate(() =>
            {
                item.position = Vector3.Lerp(startPos, WorldPosition, t);
                item.localScale = Vector3.Lerp(startScale, Vector3.one * shrinkTo, t);
            })
            .OnComplete(() => Destroy(item.gameObject));
    }
}
