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

    // UI 目标对应的世界坐标
    public Vector3 WorldPosition
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

    // 供 Gold / ItemAbout 调用
    public void AnimatePickup(Transform item)
    {
        Vector3 targetPos = WorldPosition;
        if (targetPos == Vector3.zero) { Destroy(item.gameObject); return; }

        item.DOScale(shrinkTo, flyDuration).SetEase(flyEase);
        item.DOMove(targetPos, flyDuration).SetEase(flyEase).OnComplete(() =>
            Destroy(item.gameObject));
    }
}
