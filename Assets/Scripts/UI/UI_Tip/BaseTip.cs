using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseTip : MonoBehaviour
{
    #region 组件引用
    [Header("核心组件")]
    [SerializeField] protected TextMeshProUGUI tipText; // 提示文本
    [SerializeField] protected CanvasGroup canvasGroup; // 透明度控制组件
    [SerializeField] protected RectTransform tipRoot; // 提示框根节点（用于位置动画）
    #endregion

    #region 配置参数
    [Header("基础动画配置")]
    [SerializeField] protected float fadeInTime = 0.4f; // 淡入时长
    [SerializeField] protected float displayTime = 1.5f; // 显示时长
    [SerializeField] protected float fadeOutTime = 0.3f; // 淡出时长

    [Header("缩放动画配置")]
    [SerializeField] protected float scaleUpMultiplier = 1.2f; // 放大倍数（建议1.15~1.25）
    [SerializeField] protected float scaleDownMultiplier = 0.9f; // 缩小倍数（建议0.85~0.95）
    [Range(0.5f, 0.8f)]
    [SerializeField] protected float scalePeakTime = 0.7f; // 缩放峰值时间占比（0.7=70%淡入时间到最大）

    [Header("滑入滑出动画配置")]
    [SerializeField] protected bool enableSlideAnimation = true; // 启用滑入滑出动画
    [SerializeField] protected float slideDistance = 200f; // 滑动距离（像素）
    [SerializeField] protected SlideDirection slideDirection = SlideDirection.FromLeft; // 滑动方向

    [Header("抖动效果配置")]
    [SerializeField] protected bool enableShake = true; // 启用淡入完成后的抖动效果
    [SerializeField] protected float shakeIntensity = 3f; // 抖动强度（像素）
    [SerializeField] protected float shakeDuration = 0.3f; // 抖动时长
    [SerializeField] protected float shakeFrequency = 15f; // 抖动频率

    [Header("颜色配置")]
    [SerializeField] protected Color successColor = new Color(1, 1, 1); // 成功提示主色（白色）
    [SerializeField] protected Color errorColor = new Color(1, 0, 0); // 错误提示主色（红色）

    [Header("动画效果配置")]
    [SerializeField] protected AnimationType animationType = AnimationType.滑动; // 动画效果类型

    [Header("队列配置")]
    [SerializeField] protected int maxQueueSize = 5; // 最大队列长度（超过则丢弃旧提示）
    #endregion

    #region 枚举定义
    // 动画效果类型枚举
    public enum AnimationType
    {
        基础,    // 基础效果：淡入淡出+缩放
        滑动      // 滑动效果：滑入滑出+缩放
    }

    // 滑动方向枚举
    protected enum SlideDirection
    {
        FromLeft,    // 从左侧滑入，向右侧滑出
        FromTop      // 从上方滑入，向上方滑出
    }
    #endregion

    #region 成员变量
    protected Coroutine currentCoroutine; // 当前动画协程
    protected List<(string message, bool isSuccess, AnimationType? animType, bool? enableShakeParam)> tipQueue; // 提示队列（支持优先级）
    protected bool isPlaying; // 是否正在播放提示
    protected Vector3 originalScale; // 初始缩放比例
    protected Vector2 originalPos; // 初始位置
    #endregion

    #region 初始化
    protected virtual void Awake()
    {
        // 初始化队列
        tipQueue = new List<(string, bool, AnimationType?, bool?)>();

        // 初始化隐藏状态
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
        }

        // 记录初始状态
        if (tipRoot != null)
        {
            originalScale = tipRoot.localScale;
            originalPos = tipRoot.anchoredPosition;
        }
    }
    #endregion

    #region 公共接口
    // 统一提示入口（区分成功/失败类型）
    public void ShowTip(string message, bool isSuccess, AnimationType? animType = null, bool? enableShakeParam = null)
    {
        if (tipText == null || canvasGroup == null || tipRoot == null) return;

        // 如果正在播放，加入队列
        if (isPlaying)
        {
            EnqueueTip(message, isSuccess, animType, enableShakeParam);
            return;
        }

        // 直接播放
        PlayTip(message, isSuccess, animType, enableShakeParam);
    }

    // 强制隐藏提示框（场景切换/面板关闭时调用）
    public void ForceHideTip()
    {
        // 停止当前动画
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }

        // 清空队列
        if (tipQueue != null)
        {
            tipQueue.Clear();
        }

        // 重置状态
        isPlaying = false;
        if (canvasGroup != null) canvasGroup.alpha = 0;
        if (tipRoot != null)
        {
            tipRoot.anchoredPosition = originalPos;
            tipRoot.localScale = originalScale;
        }
    }
    #endregion

    #region 队列管理
    // 将提示加入队列（带抖动的提示优先显示）
    private void EnqueueTip(string message, bool isSuccess, AnimationType? animType = null, bool? enableShakeParam = null)
    {
        // 如果队列已满，移除最早的提示
        if (tipQueue.Count >= maxQueueSize)
        {
            tipQueue.RemoveAt(0);
            Debug.LogWarning("提示队列已满，丢弃最早的提示");
        }

        // 判断是否为紧急提示（启用抖动）
        bool isUrgent = enableShakeParam ?? enableShake;

        if (isUrgent)
        {
            // 紧急提示：插入到队列前面（第一个位置）
            tipQueue.Insert(0, (message, isSuccess, animType, enableShakeParam));
        }
        else
        {
            // 普通提示：添加到队列后面
            tipQueue.Add((message, isSuccess, animType, enableShakeParam));
        }
    }

    // 播放提示
    private void PlayTip(string message, bool isSuccess, AnimationType? animType = null, bool? enableShakeParam = null)
    {
        isPlaying = true;

        // 重置所有状态
        canvasGroup.alpha = 0;
        tipText.text = message;
        tipRoot.localScale = originalScale;
        tipRoot.anchoredPosition = originalPos;

        // 启动组合动画
        currentCoroutine = StartCoroutine(CombinedAnimation(isSuccess, animType, enableShakeParam));
    }

    // 播放下一个提示
    private void PlayNextTip()
    {
        if (tipQueue.Count > 0)
        {
            var nextTip = tipQueue[0];
            tipQueue.RemoveAt(0);
            PlayTip(nextTip.message, nextTip.isSuccess, nextTip.animType, nextTip.enableShakeParam);
        }
        else
        {
            isPlaying = false;
        }
    }
    #endregion

    #region 主动画流程
    // 组合动画：根据动画类型执行不同的效果组合
    private IEnumerator CombinedAnimation(bool isSuccess, AnimationType? animType = null, bool? enableShakeParam = null)
    {
        // 使用传入的参数，如果没有则使用默认值
        AnimationType currentAnimType = animType ?? animationType;
        bool currentEnableShake = enableShakeParam ?? enableShake;

        // 初始化颜色
        Color mainColor = isSuccess ? successColor : errorColor;
        tipText.color = mainColor;

        // 计算滑入滑出的起始和结束位置
        Vector2 slideInStartPos = GetSlideInStartPosition();
        Vector2 slideOutEndPos = GetSlideOutEndPosition();

        // 根据动画类型执行不同的动画组合
        switch (currentAnimType)
        {
            case AnimationType.基础:
                // 基础效果：淡入淡出+缩放（可选抖动）
                yield return StartCoroutine(BasicAnimation(currentEnableShake));
                break;

            case AnimationType.滑动:
                // 滑动效果：滑入滑出+缩放（可选抖动）
                yield return StartCoroutine(SlideAnimation(slideInStartPos, slideOutEndPos, currentEnableShake));
                break;
        }

        // ========== 重置状态 ==========
        ResetTipState();

        // 播放下一个提示
        PlayNextTip();
    }

    // 淡入动画
    private IEnumerator FadeInAnimation(Vector2 startPos)
    {
        float timer = 0;
        canvasGroup.blocksRaycasts = true;
        tipRoot.anchoredPosition = startPos; // 从起始位置开始

        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            float totalProgress = Mathf.Clamp01(timer / fadeInTime);

            // 透明度：线性淡入
            canvasGroup.alpha = totalProgress;

            // 如果起始位置不等于目标位置，执行滑入动画
            if (startPos != originalPos)
            {
                tipRoot.anchoredPosition = Vector2.Lerp(
                    startPos,
                    originalPos,
                    Mathf.SmoothStep(0, 1, totalProgress)
                );
            }

            // 缩放动画：先放大后回正
            UpdateScaleAnimation(totalProgress);

            yield return null;
        }

        // 淡入结束：强制归位到基准状态
        canvasGroup.alpha = 1;
        tipRoot.localScale = originalScale;
        tipRoot.anchoredPosition = originalPos;
    }

    // 保持显示动画
    private IEnumerator KeepDisplayAnimation()
    {
        float floatTimer = 0;
        while (floatTimer < displayTime)
        {
            floatTimer += Time.deltaTime;
            tipRoot.anchoredPosition = originalPos;
            yield return null;
        }
    }

    // 淡出动画
    private IEnumerator FadeOutAnimation(Vector2 endPos)
    {
        float timer = 0;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float totalProgress = Mathf.Clamp01(timer / fadeOutTime);

            // 透明度：缓出淡出
            canvasGroup.alpha = Mathf.Lerp(1, 0, Mathf.SmoothStep(0, 1, totalProgress));

            // 缩放：缓出缩小
            tipRoot.localScale = originalScale * Mathf.Lerp(1f, scaleDownMultiplier, Mathf.SmoothStep(0, 1, totalProgress));

            // 如果结束位置不等于目标位置，执行滑出动画
            if (endPos != originalPos)
            {
                tipRoot.anchoredPosition = Vector2.Lerp(
                    originalPos,
                    endPos,
                    Mathf.SmoothStep(0, 1, totalProgress)
                );
            }

            yield return null;
        }
    }

    // 重置提示状态
    private void ResetTipState()
    {
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
        tipRoot.localScale = originalScale;
        tipRoot.anchoredPosition = originalPos;
        currentCoroutine = null;
    }

    // ========== 不同动画效果的实现 ==========

    // 基础效果：淡入淡出+缩放（可选抖动）
    private IEnumerator BasicAnimation(bool enableShakeParam)
    {
        // 淡入阶段
        yield return StartCoroutine(FadeInAnimation(originalPos));

        // 抖动效果
        if (enableShakeParam)
        {
            yield return StartCoroutine(ShakeEffect());
        }

        // 保持阶段
        yield return StartCoroutine(KeepDisplayAnimation());

        // 淡出阶段
        yield return StartCoroutine(FadeOutAnimation(originalPos));
    }

    // 滑动效果：滑入滑出+缩放（可选抖动）
    private IEnumerator SlideAnimation(Vector2 slideInStartPos, Vector2 slideOutEndPos, bool enableShakeParam)
    {
        // 滑入+淡入阶段
        yield return StartCoroutine(FadeInAnimation(slideInStartPos));

        // 抖动效果
        if (enableShakeParam)
        {
            yield return StartCoroutine(ShakeEffect());
        }

        // 保持阶段
        yield return StartCoroutine(KeepDisplayAnimation());

        // 滑出+淡出阶段
        yield return StartCoroutine(FadeOutAnimation(slideOutEndPos));
    }
    #endregion

    #region 滑入滑出动画
    // 获取滑入起始位置
    private Vector2 GetSlideInStartPosition()
    {
        if (!enableSlideAnimation) return originalPos;

        switch (slideDirection)
        {
            case SlideDirection.FromLeft:
                return originalPos + Vector2.left * slideDistance;
            case SlideDirection.FromTop:
                return originalPos + Vector2.up * slideDistance;
            default:
                return originalPos;
        }
    }

    // 获取滑出结束位置
    private Vector2 GetSlideOutEndPosition()
    {
        if (!enableSlideAnimation) return originalPos;

        switch (slideDirection)
        {
            case SlideDirection.FromLeft:
                return originalPos + Vector2.right * slideDistance;
            case SlideDirection.FromTop:
                return originalPos + Vector2.up * slideDistance;
            default:
                return originalPos;
        }
    }
    #endregion

    #region 缩放动画
    // 更新缩放动画（淡入阶段）
    private void UpdateScaleAnimation(float totalProgress)
    {
        float scaleProgress = 0;
        if (totalProgress < scalePeakTime)
        {
            // 第一段：从基准到最大缩放（缓入）
            float segmentProgress = totalProgress / scalePeakTime;
            scaleProgress = Mathf.SmoothStep(0, 1, segmentProgress);
        }
        else
        {
            // 第二段：从最大缩放回基准（缓出）
            float segmentProgress = (totalProgress - scalePeakTime) / (1 - scalePeakTime);
            scaleProgress = Mathf.SmoothStep(1, 0, segmentProgress);
        }
        // 基于原始scale的倍数缩放
        tipRoot.localScale = originalScale * Mathf.Lerp(1f, scaleUpMultiplier, scaleProgress);
    }
    #endregion

    #region 抖动效果
    // 抖动效果
    private IEnumerator ShakeEffect()
    {
        float timer = 0;
        Vector2 basePos = originalPos;

        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / shakeDuration;
            
            // 计算当前抖动强度（随时间衰减）
            float currentIntensity = shakeIntensity * (1f - progress);
            
            // 生成随机抖动偏移
            float offsetX = Mathf.Sin(timer * shakeFrequency) * currentIntensity;
            float offsetY = Mathf.Cos(timer * shakeFrequency * 1.3f) * currentIntensity;
            
            tipRoot.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
            
            yield return null;
        }

        // 恢复到基准位置
        tipRoot.anchoredPosition = basePos;
    }
    #endregion
}
