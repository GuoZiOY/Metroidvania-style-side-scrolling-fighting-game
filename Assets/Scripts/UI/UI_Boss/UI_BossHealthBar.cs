using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss 顶部大血条（公共）——绑定任意 Boss 的 Entity_Health，显示名称+血量填充+缓冲条（受损延迟追赶）
// 效果与敌人局内血条一致（Slider 主条 + 缓冲残影），但不做翻转/跟随——固定顶部 HUD 条
// Canvas 结构复用敌人血条预制体（Slider + Fill Area[缓冲条/Fill] + 名字），复制进 BossHUD 后接线
public class UI_BossHealthBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;   // Boss 名称文本
    [SerializeField] private Slider slider;              // 主血条 Slider（驱动 Fill）
    [SerializeField] private Image bufferImage;          // 缓冲条（受损后延迟追赶，显示伤害残影）
    [SerializeField] private CanvasGroup canvasGroup;    // 整条显隐/淡入控制

    [Header("缓冲效果")]
    [SerializeField] private float bufferDelay = 0.5f;    // 缓冲延迟（受伤后）
    [SerializeField] private float bufferDuration = 0.5f; // 缓冲追赶时长

    private Entity_Health boundHealth; // 当前绑定的 Boss 生命
    private Coroutine bufferCo;        // 缓冲追赶协程

    // 绑定 Boss：订阅生命更新，立即刷新到初始值
    public void BindBoss(Entity_Health health, string bossName)
    {
        Unbind();
        boundHealth = health;
        if (boundHealth != null)
            boundHealth.OnHealthUpdate += OnHealthUpdated;
        if (nameText != null)
            nameText.text = bossName;
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        if (slider != null)
            slider.value = 1f;
        if (bufferImage != null)
            bufferImage.fillAmount = 1f;
        OnHealthUpdated(); // 立即刷新（100%）
    }

    // 解绑：Boss 死亡（OnEntityDead）时调用，防销毁后迟到回调 MissingReferenceException
    public void Unbind()
    {
        if (boundHealth != null)
            boundHealth.OnHealthUpdate -= OnHealthUpdated;
        boundHealth = null;
        if (bufferCo != null)
        {
            StopCoroutine(bufferCo);
            bufferCo = null;
        }
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnHealthUpdated()
    {
        if (boundHealth == null)
            return;
        float percent = boundHealth.GetHealthPercent();
        if (slider != null)
            slider.value = percent;

        if (bufferImage == null)
            return;
        if (percent > bufferImage.fillAmount + 0.001f)
        {
            // 回血（分裂回血/隐身回血）：缓冲条同步跟上
            bufferImage.fillAmount = percent;
        }
        else if (percent < bufferImage.fillAmount - 0.001f)
        {
            // 受伤：主条即时掉，缓冲条延迟追赶（伤害残影）
            if (bufferCo != null)
                StopCoroutine(bufferCo);
            bufferCo = StartCoroutine(BufferCo(percent));
        }
    }

    // 缓冲追赶：延迟 bufferDelay 后，bufferDuration 内从旧值 lerp 到目标
    private IEnumerator BufferCo(float targetPercent)
    {
        float startFill = bufferImage.fillAmount;
        yield return new WaitForSeconds(bufferDelay);

        float elapsed = 0f;
        while (elapsed < bufferDuration)
        {
            elapsed += Time.deltaTime;
            bufferImage.fillAmount = Mathf.Lerp(startFill, targetPercent, elapsed / bufferDuration);
            yield return null;
        }
        bufferImage.fillAmount = targetPercent;
        bufferCo = null;
    }
}
