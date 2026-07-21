using UnityEngine;
using TMPro;
using DG.Tweening;
using static Entity_Stats;

public enum PopUpType
{
    NormalDamage,
    CriticalDamage,
    SkillTip
}

public class PopUpText : MonoBehaviour
{
    [SerializeField] private TextMeshPro myText;

    [Header("弹出类型")]
    private PopUpType popUpType = PopUpType.NormalDamage;

    [Header("普通伤害参数 - 抛物线运动")]
    [SerializeField] private float horizontalSpeed = 3f;
    [SerializeField] private float upwardSpeed = 5f;
    [SerializeField] private float gravity = 8f;

    [Header("弹性缩放参数")]
    [SerializeField] private float startScale = 0.1f;
    [SerializeField] private float maxScale = 1.3f;
    [SerializeField] private float targetScale = 1f;
    [SerializeField] private float scaleDuration = 0.4f;
    [SerializeField] private float elasticity = 0.6f;

    [Header("旋转参数")]
    [SerializeField] private float maxRotation = 15f;

    [Header("震动参数")]
    [SerializeField] private bool enableShake = true;
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeDuration = 0.3f;

    [Header("暴击伤害参数 - 颜色闪烁")]
    [SerializeField] private bool enableColorFlash = false;
    [SerializeField] private float colorFlashSpeed = 25f;
    [SerializeField] private float colorFlashDuration = 1f;
    [SerializeField] private Color flashColor1 = Color.white;
    [SerializeField] private Color flashColor2 = Color.red;

    [Header("暴击伤害参数 - 缩放")]
    [SerializeField] private float criticalScaleMultiplier = 1.5f;
    [SerializeField] private float criticalScaleDurationMultiplier = 1.5f;
    [SerializeField] private float criticalElasticityMultiplier = 1.5f;

    [Header("位置偏移参数")]
    [SerializeField] private float textOffsetX = 1f;
    [SerializeField] private float textOffsetY = 1.5f;

    [Header("暴击伤害位置参数")]
    [SerializeField] private float criticalOffsetX = 1.5f;
    [SerializeField] private float criticalOffsetY = 2.5f;

    [Header("停留参数")]
    [SerializeField] private float stayDuration = 0.4f;

    [Header("消失动画参数")]
    [SerializeField] private float disappearUpwardSpeed = 2f;
    [SerializeField] private float disappearSpeed = 3f;
    [SerializeField] private float disappearRotationSpeed = 30f;

    private float scaleMultiplier = 1f;
    private Color originalColor;
    private Tween _mainTween;
    private Tween _shakeTween;
    private const float DISAPPEAR_DURATION = 0.5f;

    private void Start()
    {
        myText = GetComponent<TextMeshPro>();
        originalColor = myText.color;
        transform.localScale = Vector3.one * startScale;

        switch (popUpType)
        {
            case PopUpType.NormalDamage:
            case PopUpType.SkillTip:
                PlayNormalDamage();
                break;
            case PopUpType.CriticalDamage:
                PlayCriticalDamage();
                break;
        }
    }

    // ==================== 普通伤害 ====================

    private void PlayNormalDamage()
    {
        int dir = Random.Range(0, 2) == 0 ? -1 : 1;
        float rotZ = Random.Range(-maxRotation, maxRotation);
        transform.rotation = Quaternion.Euler(0, 0, rotZ);
        Vector3 startPos = transform.position;
        float totalDuration = scaleDuration + stayDuration + DISAPPEAR_DURATION;

        // 记录消失开始时的基准位置和旋转，用于不依赖 Time.deltaTime 的时间推导
        Vector3 disappearBasePos = default;
        float accumulatedRotation = 0f;

        _mainTween = DOTween.To(() => 0f, t =>
        {
            if (t <= scaleDuration)
            {
                // Phase 1: 抛物线 + 弹性缩放 + 旋转回正（0~scaleDuration）
                float x = startPos.x + dir * horizontalSpeed * t;
                float y = startPos.y + upwardSpeed * t - 0.5f * gravity * t * t;
                transform.position = new Vector3(x, y, 0);

                // 弹性缩放（原公式）
                float p = t / scaleDuration;
                float val;
                if (p < 0.5f)
                    val = Mathf.Lerp(startScale, maxScale, EaseOutBack(p * 2f, elasticity));
                else
                    val = Mathf.Lerp(maxScale, targetScale, EaseOutElastic((p - 0.5f) * 2f, elasticity));
                transform.localScale = Vector3.one * val * scaleMultiplier;

                // 旋转回正
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(rotZ, 0, EaseOutQuad(p)));
            }
            else
            {
                float dt = t - scaleDuration - stayDuration;
                if (dt > 0)
                {
                    // Phase 3: 消失 — 基于消失时间推导位置和旋转，不依赖 Time.deltaTime
                    float dp = Mathf.Clamp01(dt / DISAPPEAR_DURATION);
                    if (dp < 0.01f)
                    {
                        disappearBasePos = transform.position;
                        accumulatedRotation = transform.rotation.eulerAngles.z;
                    }

                    // 位置 = 基准 + 上浮距离 × 进度
                    transform.position = disappearBasePos + new Vector3(0, disappearUpwardSpeed * DISAPPEAR_DURATION * dp, 0);
                    // 旋转 = 基准 + 累计旋转 × 进度
                    transform.rotation = Quaternion.Euler(0, 0, accumulatedRotation + disappearRotationSpeed * DISAPPEAR_DURATION * dp);

                    // 渐隐
                    Color c = myText.color;
                    myText.color = new Color(c.r, c.g, c.b, 1f - dp);
                }
            }
        }, totalDuration, totalDuration)
            .SetEase(Ease.Linear)
            .OnComplete(() => Destroy(gameObject));
    }

    // ==================== 暴击伤害 ====================

    private void PlayCriticalDamage()
    {
        float rotZ = Random.Range(-maxRotation, maxRotation);
        transform.rotation = Quaternion.Euler(0, 0, rotZ);
        Vector3 startPos = transform.position;

        float tDur = scaleDuration * criticalScaleDurationMultiplier;
        float curElasticity = elasticity * criticalElasticityMultiplier;
        float totalDuration = tDur + stayDuration + DISAPPEAR_DURATION;

        Vector3 disappearBasePos = default;
        float accumulatedRotation = 0f;

        _mainTween = DOTween.To(() => 0f, t =>
        {
            if (t <= tDur)
            {
                // Phase 1: 弹性缩放 + 抖动 + 颜色闪烁
                float p = t / tDur;
                float val;
                if (p < 0.5f)
                    val = Mathf.Lerp(startScale, maxScale, EaseOutBack(p * 2f, curElasticity));
                else
                    val = Mathf.Lerp(maxScale, targetScale, EaseOutElastic((p - 0.5f) * 2f, curElasticity));
                transform.localScale = Vector3.one * val * scaleMultiplier;

                // 抖动（独立 tween，每帧随机偏移，和原版完全一致）
                if (enableShake && shakeDuration > 0 && t <= Time.deltaTime && _shakeTween == null)
                {
                    Vector3 shakeBasePos = transform.position;
                    _shakeTween = DOTween.To(() => 0f, st =>
                    {
                        float intensity = shakeIntensity * (1f - st / shakeDuration);
                        transform.position = shakeBasePos + new Vector3(
                            Random.Range(-intensity, intensity),
                            Random.Range(-intensity, intensity), 0);
                    }, shakeDuration, shakeDuration).SetEase(Ease.Linear)
                    .OnComplete(() => { transform.position = shakeBasePos; _shakeTween = null; });
                }

                // 颜色闪烁（原版 sin 波）
                if (enableColorFlash)
                {
                    float fp = (Mathf.Sin(t * colorFlashSpeed) + 1f) / 2f;
                    myText.color = Color.Lerp(flashColor1, flashColor2, fp);
                }
            }
            else
            {
                float dt = t - tDur - stayDuration;
                if (dt > 0)
                {
                    float dp = Mathf.Clamp01(dt / DISAPPEAR_DURATION);
                    if (dp < 0.01f)
                    {
                        disappearBasePos = transform.position;
                        accumulatedRotation = transform.rotation.eulerAngles.z;
                    }

                    transform.position = disappearBasePos + new Vector3(0, disappearUpwardSpeed * DISAPPEAR_DURATION * dp, 0);
                    transform.rotation = Quaternion.Euler(0, 0, accumulatedRotation + disappearRotationSpeed * DISAPPEAR_DURATION * dp);

                    Color c = myText.color;
                    if (enableColorFlash)
                    {
                        float fp = (Mathf.Sin(t * colorFlashSpeed) + 1f) / 2f;
                        c = Color.Lerp(flashColor1, flashColor2, fp);
                    }
                    myText.color = new Color(c.r, c.g, c.b, 1f - dp);
                }
            }
        }, totalDuration, totalDuration)
            .SetEase(Ease.Linear)
            .OnComplete(() => Destroy(gameObject));
    }

    // ==================== 原版缓动函数（保持视觉完全一致） ====================

    private float EaseOutBack(float t, float e = 1f)
    {
        float c1 = 1.70158f * e;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseOutElastic(float t, float e = 1f)
    {
        float c4 = (2f * Mathf.PI) / 3f;
        float amp = 1f * e;
        return t == 0f ? 0f : t == 1f ? 1f : amp * Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private void OnDestroy() { _mainTween?.Kill(); _shakeTween?.Kill(); }

    // ==================== 公共接口 ====================

    public void SetScale(float multiplier) { scaleMultiplier = multiplier; }
    public void SetText(string text) { if (myText != null) myText.SetText(text); }
    public void SetColor(Color color) { if (myText != null) myText.color = color; }

    public void SetRandomOffsetPosition()
    {
        transform.position += new Vector3(Random.Range(-textOffsetX, textOffsetX), Random.Range(textOffsetY, textOffsetY * 2f), 0);
    }

    public void SetCriticalRandomOffsetPosition()
    {
        transform.position += new Vector3(Random.Range(-criticalOffsetX, criticalOffsetX), Random.Range(criticalOffsetY, criticalOffsetY * 2f), 0);
    }

    public void SetPopUpType(PopUpType type) { popUpType = type; }

    public void SetSkillTip(string text, Color color)
    {
        popUpType = PopUpType.SkillTip;
        SetText(text);
        SetColor(color);
        originalColor = color;
        SetRandomOffsetPosition();
    }

    public void SetCriticalDamage(float physicalDamage, float elementalDamage, ElementType element)
    {
        popUpType = PopUpType.CriticalDamage;
        SetText((physicalDamage + elementalDamage).ToString("F0"));

        if (elementalDamage > 0)
        {
            Color dc = element switch
            {
                ElementType.Fire => Color.red,
                ElementType.Ice => Color.cyan,
                ElementType.Lightning => Color.yellow,
                _ => Color.white
            };
            flashColor1 = Color.white;
            flashColor2 = dc;
            SetColor(dc);
        }
        else
        {
            flashColor1 = Color.white;
            flashColor2 = new Color(1f, 0.5f, 0f);
            SetColor(Color.white);
        }
        originalColor = myText.color;
        enableShake = true;
        enableColorFlash = true;
        scaleMultiplier = criticalScaleMultiplier;
        SetCriticalRandomOffsetPosition();
    }

    public void SetDamageText(float physicalDamage, float elementalDamage, ElementType element, bool isCrit = false)
    {
        SetText((physicalDamage + elementalDamage).ToString("F0"));
        popUpType = isCrit ? PopUpType.CriticalDamage : PopUpType.NormalDamage;

        Color dc = Color.white;
        if (elementalDamage > 0)
        {
            dc = element switch
            {
                ElementType.Fire => Color.red,
                ElementType.Ice => Color.cyan,
                ElementType.Lightning => new Color(1f, 0.5f, 0f),
                _ => Color.white
            };
            if (isCrit) { flashColor1 = Color.white; flashColor2 = dc; }
        }
        else if (isCrit) { enableColorFlash = false; }

        SetColor(dc);
        originalColor = dc;
        scaleMultiplier = isCrit ? criticalScaleMultiplier : 1f;
        if (isCrit)
        {
            enableShake = true;
            SetCriticalRandomOffsetPosition();
        }
        else
        {
            SetRandomOffsetPosition();
        }
    }
}
