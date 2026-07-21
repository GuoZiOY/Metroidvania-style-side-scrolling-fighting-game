using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public abstract class BaseTip : MonoBehaviour
{
    #region 组件引用
    [Header("核心组件")]
    [SerializeField] protected TextMeshProUGUI tipText;
    [SerializeField] protected CanvasGroup canvasGroup;
    [SerializeField] protected RectTransform tipRoot;
    #endregion

    #region 配置参数
    [Header("基础动画配置")]
    [SerializeField] protected float fadeInTime = 0.4f;
    [SerializeField] protected float displayTime = 1.5f;
    [SerializeField] protected float fadeOutTime = 0.3f;

    [Header("缩放动画配置")]
    [SerializeField] protected float scaleUpMultiplier = 1.2f;
    [SerializeField] protected float scaleDownMultiplier = 0.9f;
    [Range(0.5f, 0.8f)]
    [SerializeField] protected float scalePeakTime = 0.7f;

    [Header("滑入滑出动画配置")]
    [SerializeField] protected bool enableSlideAnimation = true;
    [SerializeField] protected float slideDistance = 200f;
    [SerializeField] protected SlideDirection slideDirection = SlideDirection.FromLeft;

    [Header("抖动效果配置")]
    [SerializeField] protected bool enableShake = true;
    [SerializeField] protected float shakeIntensity = 3f;
    [SerializeField] protected float shakeDuration = 0.3f;

    [Header("颜色配置")]
    [SerializeField] protected Color successColor = Color.white;
    [SerializeField] protected Color errorColor = Color.red;

    [Header("队列配置")]
    [SerializeField] protected int maxQueueSize = 5;
    #endregion

    [Header("动画效果类型")]
    [SerializeField] protected AnimationType animationType = AnimationType.滑动;

    public enum AnimationType { 基础, 滑动 }
    protected enum SlideDirection { FromLeft, FromTop }

    #region 成员变量
    protected Sequence currentSeq;
    protected List<(string message, bool isSuccess, AnimationType? animType, bool? enableShakeParam)> tipQueue;
    protected bool isPlaying;
    protected Vector3 originalScale;
    protected Vector2 originalPos;
    #endregion

    #region 初始化
    protected virtual void Awake()
    {
        tipQueue = new List<(string, bool, AnimationType?, bool?)>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
        }
        if (tipRoot != null)
        {
            originalScale = tipRoot.localScale;
            originalPos = tipRoot.anchoredPosition;
        }
    }
    #endregion

    #region 公共接口
    public void ShowTip(string message, bool isSuccess, AnimationType? animType = null, bool? enableShakeParam = null)
    {
        if (tipText == null || canvasGroup == null || tipRoot == null) return;

        if (isPlaying) { EnqueueTip(message, isSuccess, animType, enableShakeParam); return; }
        PlayTip(message, isSuccess, animType, enableShakeParam);
    }

    public void ForceHideTip()
    {
        currentSeq?.Kill();
        tipQueue?.Clear();
        isPlaying = false;
        ResetTipState();
    }
    #endregion

    #region 队列管理
    private void EnqueueTip(string message, bool isSuccess, AnimationType? animType = null, bool? enableShakeParam = null)
    {
        if (tipQueue.Count >= maxQueueSize) tipQueue.RemoveAt(0);
        bool isUrgent = enableShakeParam ?? enableShake;
        if (isUrgent) tipQueue.Insert(0, (message, isSuccess, animType, enableShakeParam));
        else tipQueue.Add((message, isSuccess, animType, enableShakeParam));
    }

    private void PlayTip(string message, bool isSuccess, AnimationType? animType = null, bool? enableShakeParam = null)
    {
        isPlaying = true;
        ResetTipState();
        tipText.text = message;

        Color mainColor = isSuccess ? successColor : errorColor;
        tipText.color = mainColor;

        AnimationType currentAnimType = animType ?? animationType;
        bool currentEnableShake = enableShakeParam ?? enableShake;

        currentSeq = DOTween.Sequence();
        currentSeq.SetAutoKill(true);

        switch (currentAnimType)
        {
            case AnimationType.基础:
                BuildBasicAnimation(currentSeq, currentEnableShake);
                break;
            case AnimationType.滑动:
                BuildSlideAnimation(currentSeq, currentEnableShake);
                break;
        }

        currentSeq.OnComplete(() => { ResetTipState(); PlayNextTip(); });
    }

    private void PlayNextTip()
    {
        if (tipQueue.Count > 0)
        {
            var next = tipQueue[0];
            tipQueue.RemoveAt(0);
            PlayTip(next.message, next.isSuccess, next.animType, next.enableShakeParam);
        }
        else isPlaying = false;
    }
    #endregion

    #region 动画构建
    private void BuildBasicAnimation(Sequence seq, bool enableShakeParam)
    {
        // 淡入 + 弹性缩放（原版先放大后回正）
        seq.Append(canvasGroup.DOFade(1, fadeInTime));
        seq.Join(tipRoot.DOScale(originalScale * scaleUpMultiplier, fadeInTime).SetEase(Ease.OutBack, 1.5f));
        // 缩放到基准
        seq.Join(tipRoot.DOScale(originalScale, fadeInTime * (1f - scalePeakTime)).SetDelay(fadeInTime * scalePeakTime));

        if (enableShakeParam)
            seq.Append(DOTween.To(() => 0f, st =>
            {
                float progress = st / shakeDuration;
                float intensity = shakeIntensity * (1f - progress);
                tipRoot.anchoredPosition = originalPos + new Vector2(
                    Mathf.Sin(st * 30f) * intensity,
                    Mathf.Cos(st * 39f) * intensity);
            }, shakeDuration, shakeDuration).SetEase(Ease.Linear));

        seq.AppendInterval(displayTime);

        // 淡出 + 缩小
        seq.Append(canvasGroup.DOFade(0, fadeOutTime));
        seq.Join(tipRoot.DOScale(originalScale * scaleDownMultiplier, fadeOutTime).SetEase(Ease.OutQuad));
        seq.Join(tipRoot.DOAnchorPos(originalPos, fadeOutTime));
    }

    private void BuildSlideAnimation(Sequence seq, bool enableShakeParam)
    {
        Vector2 slideInStart = GetSlideInStartPosition();
        Vector2 slideOutEnd = GetSlideOutEndPosition();

        // 滑入 + 淡入 + 缩放
        tipRoot.anchoredPosition = slideInStart;
        seq.Append(canvasGroup.DOFade(1, fadeInTime));
        seq.Join(tipRoot.DOAnchorPos(originalPos, fadeInTime).SetEase(Ease.OutQuad));
        seq.Join(tipRoot.DOScale(originalScale * scaleUpMultiplier, fadeInTime).SetEase(Ease.OutBack, 1.5f));

        if (enableShakeParam)
            seq.Append(DOTween.To(() => 0f, st =>
            {
                float progress = st / shakeDuration;
                float intensity = shakeIntensity * (1f - progress);
                tipRoot.anchoredPosition = originalPos + new Vector2(
                    Mathf.Sin(st * 30f) * intensity,
                    Mathf.Cos(st * 39f) * intensity);
            }, shakeDuration, shakeDuration).SetEase(Ease.Linear));

        seq.AppendInterval(displayTime);

        // 滑出 + 淡出 + 缩小
        seq.Append(canvasGroup.DOFade(0, fadeOutTime));
        seq.Join(tipRoot.DOAnchorPos(slideOutEnd, fadeOutTime).SetEase(Ease.InQuad));
        seq.Join(tipRoot.DOScale(originalScale * scaleDownMultiplier, fadeOutTime).SetEase(Ease.OutQuad));
    }
    #endregion

    #region 辅助
    private Vector2 GetSlideInStartPosition()
    {
        if (!enableSlideAnimation) return originalPos;
        return slideDirection switch
        {
            SlideDirection.FromLeft => originalPos + Vector2.left * slideDistance,
            SlideDirection.FromTop => originalPos + Vector2.up * slideDistance,
            _ => originalPos
        };
    }

    private Vector2 GetSlideOutEndPosition()
    {
        if (!enableSlideAnimation) return originalPos;
        return slideDirection switch
        {
            SlideDirection.FromLeft => originalPos + Vector2.right * slideDistance,
            SlideDirection.FromTop => originalPos + Vector2.up * slideDistance,
            _ => originalPos
        };
    }

    private void ResetTipState()
    {
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
        tipRoot.localScale = originalScale;
        tipRoot.anchoredPosition = originalPos;
    }
    #endregion
}
