using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
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
    [SerializeField] private bool enableShake = false;
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeDuration = 0.3f;

    [Header("暴击伤害参数 - 颜色闪烁")]
    [SerializeField] private bool enableColorFlash = false;
    [SerializeField] private float colorFlashSpeed = 25f;
    [SerializeField] private float colorFlashDuration = 1f;
    [SerializeField] private Color flashColor1 = Color.white;
    [SerializeField] private Color flashColor2 = Color.red;

    [Header("暴击伤害参数 - 缩放")]
    [SerializeField] private float criticalScaleMultiplier = 1.5f;//暴击伤害缩放倍数
    [SerializeField] private float criticalScaleDurationMultiplier = 1.5f;//暴击伤害缩放持续时间倍数
    [SerializeField] private float criticalElasticityMultiplier = 1.5f;//暴击伤害缩放弹性倍数   

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

    private float timer;
    private float scaleTimer;
    private float rotationTimer;
    private Vector3 velocity;
    private int horizontalDirection;
    private bool isDisappearing;
    private float currentRotation;
    private float shakeTimer;
    private float scaleMultiplier = 1f;
    private Vector3 originalPosition;
    private bool directionSet = false;
    private Color originalColor;
    private float colorFlashTimer;
    private float colorFlashElapsedTimer;

    private void Start()
    {
        myText = GetComponent<TextMeshPro>();
        originalColor = myText.color;
        originalPosition = transform.position;
        
        transform.localScale = Vector3.one * startScale;
        
        InitializeNormalDamage();
    }

    private void InitializeNormalDamage()
    {
        if (!directionSet)
        {
            horizontalDirection = Random.Range(0, 2) == 0 ? -1 : 1;
        }
        
        velocity = new Vector3(horizontalDirection * horizontalSpeed, upwardSpeed, 0);
        currentRotation = Random.Range(-maxRotation, maxRotation);
    }

    private void Update()
    {
        if (!isDisappearing)
        {
            switch (popUpType)
            {
                case PopUpType.NormalDamage:
                case PopUpType.SkillTip:
                    UpdateNormalDamage();
                    break;
                case PopUpType.CriticalDamage:
                    UpdateCriticalDamage();
                    break;
            }
            
            timer += Time.deltaTime;
            
            float currentScaleDuration = scaleDuration;
            if (popUpType == PopUpType.CriticalDamage)
            {
                currentScaleDuration *= criticalScaleDurationMultiplier;
            }
            
            if (timer >= currentScaleDuration + stayDuration)
            {
                StartDisappearAnimation();
            }
        }
        else
        {
            UpdateDisappearAnimation();
        }
    }

    private void UpdateNormalDamage()
    {
        UpdateParabolicMotion();
        UpdateElasticScale();
        UpdateRotation();
        
        if (enableShake)
            UpdateShake();
    }

    private void UpdateCriticalDamage()
    {
        UpdateElasticScale();
        UpdateShake();
        UpdateColorFlash();
    }

    private void UpdateParabolicMotion()
    {
        if (timer < scaleDuration)
        {
            transform.position += velocity * Time.deltaTime;
            velocity.y -= gravity * Time.deltaTime;
        }
    }

    private void UpdateElasticScale()
    {
        float currentScaleDuration = scaleDuration;
        float currentElasticity = elasticity;
        
        if (popUpType == PopUpType.CriticalDamage)
        {
            currentScaleDuration *= criticalScaleDurationMultiplier;
            currentElasticity *= criticalElasticityMultiplier;
        }
        
        if (scaleTimer < currentScaleDuration)
        {
            scaleTimer += Time.deltaTime;
            float progress = scaleTimer / currentScaleDuration;
            
            float elasticScale;
            if (progress < 0.5f)
            {
                float t = progress * 2f;
                elasticScale = Mathf.Lerp(startScale, maxScale, EaseOutBack(t, currentElasticity));
            }
            else
            {
                float t = (progress - 0.5f) * 2f;
                elasticScale = Mathf.Lerp(maxScale, targetScale, EaseOutElastic(t, currentElasticity));
            }
            
            transform.localScale = Vector3.one * elasticScale * scaleMultiplier;
        }
    }

    private void UpdateRotation()
    {
        if (rotationTimer < scaleDuration)
        {
            rotationTimer += Time.deltaTime;
            float progress = rotationTimer / scaleDuration;
            float targetRotation = Mathf.Lerp(currentRotation, 0f, EaseOutQuad(progress));
            transform.rotation = Quaternion.Euler(0, 0, targetRotation);
        }
    }

    private void UpdateShake()
    {
        if (shakeTimer < shakeDuration)
        {
            shakeTimer += Time.deltaTime;
            float intensity = shakeIntensity * (1f - shakeTimer / shakeDuration);
            Vector3 shakeOffset = new Vector3(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity),
                0
            );
            transform.position = originalPosition + shakeOffset;
        }
    }

    private void UpdateColorFlash()
    {
        if (enableColorFlash)
        {
            colorFlashElapsedTimer += Time.deltaTime;
            
            if (colorFlashElapsedTimer < colorFlashDuration)
            {
                colorFlashTimer += Time.deltaTime * colorFlashSpeed;
                float flashProgress = (Mathf.Sin(colorFlashTimer) + 1f) / 2f;
                Color flashColor = Color.Lerp(flashColor1, flashColor2, flashProgress);
                myText.color = new Color(flashColor.r, flashColor.g, flashColor.b, myText.color.a);
            }
            else
            {
                myText.color = new Color(originalColor.r, originalColor.g, originalColor.b, myText.color.a);
            }
        }
    }

    private void StartDisappearAnimation()
    {
        isDisappearing = true;
        velocity = Vector3.zero;
    }

    private void UpdateDisappearAnimation()
    {
        transform.position += Vector3.up * disappearUpwardSpeed * Time.deltaTime;
        transform.Rotate(0, 0, disappearRotationSpeed * Time.deltaTime);
        
        if (enableColorFlash && popUpType == PopUpType.CriticalDamage && colorFlashElapsedTimer < colorFlashDuration)
        {
            colorFlashTimer += Time.deltaTime * colorFlashSpeed;
            float flashProgress = (Mathf.Sin(colorFlashTimer) + 1f) / 2f;
            Color flashColor = Color.Lerp(flashColor1, flashColor2, flashProgress);
            float alpha = myText.color.a - disappearSpeed * Time.deltaTime;
            myText.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
        }
        else
        {
            float alpha = myText.color.a - disappearSpeed * Time.deltaTime;
            myText.color = new Color(myText.color.r, myText.color.g, myText.color.b, alpha);
        }

        if (myText.color.a <= 0)
        {
            Destroy(gameObject);
        }
    }

    private float EaseOutBack(float t, float elasticity = 1f)
    {
        float c1 = 1.70158f * elasticity;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseOutElastic(float t, float elasticity = 1f)
    {
        float c4 = (2f * Mathf.PI) / 3f;
        float amplitude = 1f * elasticity;
        return t == 0f ? 0f : t == 1f ? 1f : amplitude * Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    public void SetScale(float multiplier)
    {
        scaleMultiplier = multiplier;
    }

    public void EnableShake()
    {
        enableShake = true;
    }

    public void SetDirectionFromOffset(float xOffset)
    {
        if (xOffset < 0)
        {
            horizontalDirection = -1;
        }
        else if (xOffset > 0)
        {
            horizontalDirection = 1;
        }
        else
        {
            horizontalDirection = Random.Range(0, 2) == 0 ? -1 : 1;
        }
        
        directionSet = true;
        
        if (velocity != Vector3.zero)
        {
            velocity = new Vector3(horizontalDirection * horizontalSpeed, upwardSpeed, 0);
        }
    }

    public void SetText(string text)
    {
        if (myText != null)
        {
            myText.SetText(text);
        }
    }

    public void SetColor(Color color)
    {
        if (myText != null)
        {
            myText.color = color;
        }
    }

    public void SetRandomOffsetPosition()
    {
        float randomXOffset = Random.Range(-textOffsetX, textOffsetX);
        float randomYOffset = Random.Range(textOffsetY, textOffsetY * 2f);
        
        transform.position += new Vector3(randomXOffset, randomYOffset, 0);
        originalPosition = transform.position;
        
        SetDirectionFromOffset(randomXOffset);
    }

    public void SetCriticalRandomOffsetPosition()
    {
        float randomXOffset = Random.Range(-criticalOffsetX, criticalOffsetX);
        float randomYOffset = Random.Range(criticalOffsetY, criticalOffsetY * 2f);
        
        transform.position += new Vector3(randomXOffset, randomYOffset, 0);
        originalPosition = transform.position;
    }

    public void SetPopUpType(PopUpType type)
    {
        popUpType = type;
    }

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
        float totalDamage = physicalDamage + elementalDamage;
        string damageText = totalDamage.ToString("F0");
        
        popUpType = PopUpType.CriticalDamage;
        
        SetText(damageText);
        
        Color damageColor = Color.white;
        
        if (elementalDamage > 0)
        {
            switch (element)
            {
                case ElementType.Fire:
                    damageColor = Color.red;
                    break;
                case ElementType.Ice:
                    damageColor = Color.cyan;
                    break;
                case ElementType.Lightning:
                    damageColor = Color.yellow;
                    break;
            }
            
            flashColor1 = Color.white;
            flashColor2 = damageColor;
        }
        else
        {
            damageColor = Color.white;
            flashColor1 = Color.white;
            flashColor2 = new Color(1f, 0.5f, 0f);
        }
        
        SetColor(damageColor);
        originalColor = damageColor;
        
        enableShake = true;
        enableColorFlash = true;
        scaleMultiplier = criticalScaleMultiplier;
        colorFlashElapsedTimer = 0f;
        colorFlashTimer = 0f;
        SetCriticalRandomOffsetPosition();
    }

    public void SetDamageText(float physicalDamage, float elementalDamage, ElementType element, bool isCrit = false)
    {
        float totalDamage = physicalDamage + elementalDamage;
        string damageText = totalDamage.ToString("F0");
        
        SetText(damageText);
        
        popUpType = isCrit ? PopUpType.CriticalDamage : PopUpType.NormalDamage;
        
        Color damageColor = Color.white;
        
        if (elementalDamage > 0)
        {
            switch (element)
            {
                case ElementType.Fire:
                    damageColor = Color.red;
                    break;
                case ElementType.Ice:
                    damageColor = Color.cyan;
                    break;
                case ElementType.Lightning:
                    damageColor = new Color(1f, 0.5f, 0f);
                    break;
            }
            
            if (isCrit)
            {
                flashColor1 = Color.white;
                flashColor2 = damageColor;
            }
        }
        else if (isCrit)
        {
            damageColor = Color.white;
            enableColorFlash = false;
        }
        
        SetColor(damageColor);
        originalColor = damageColor;
        
        if (isCrit)
        {
            enableShake = true;
            enableColorFlash = true;
            scaleMultiplier = criticalScaleMultiplier;
            colorFlashElapsedTimer = 0f;
            colorFlashTimer = 0f;
            SetCriticalRandomOffsetPosition();
        }
        else
        {
            SetRandomOffsetPosition();
        }
    }
}
