using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("血条组件")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image bufferBar;

    [Header("缓冲效果")]
    [SerializeField] private float delayBeforeStart = 0.5f;
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("实体生命（可选，不拖则自动查找父级）")]
    [SerializeField] private Entity_Health entityHealth;
    private Coroutine bufferCo;
    private float lastSliderValue = -1f;

    private void Awake()
    {
        if (entityHealth == null)
            entityHealth = GetComponentInParent<Entity_Health>();

        // 血条 UI 不在角色层级下时，兜底自动查找玩家角色
        if (entityHealth == null)
            FindPlayerHealth();
    }

    private void Start()
    {
        if (entityHealth == null) return;
        lastSliderValue = entityHealth.GetHealthPercent();
        if (slider != null) slider.value = lastSliderValue;
        if (bufferBar != null) bufferBar.fillAmount = lastSliderValue;
    }

    private void Update()
    {
        // 玩家可能延迟生成，引用未找到时惰性重找
        if (entityHealth == null)
            FindPlayerHealth();

        if (entityHealth == null || slider == null) return;

        float current = entityHealth.GetHealthPercent();
        if (Mathf.Approximately(current, lastSliderValue))
            return;

        slider.value = current;

        if (bufferBar != null && current < lastSliderValue)
        {
            if (bufferCo != null)
                StopCoroutine(bufferCo);
            bufferCo = StartCoroutine(BufferCo(current));
        }

        lastSliderValue = current;
    }

    // 兜底自动查找玩家生命组件（血条挂在独立 UI、不在角色层级下时）
    private void FindPlayerHealth()
    {
        // 敌人血条挂在敌人层级下，不参与玩家兜底，避免误绑玩家血量
        if (GetComponentInParent<Enemy>() != null)
            return;

        var player = FindAnyObjectByType<Player>();
        if (player == null)
            return;

        entityHealth = player.GetComponentInChildren<Entity_Health>();

        // 首次找到时同步血条显示（玩家可能晚于血条生成）
        lastSliderValue = entityHealth.GetHealthPercent();
        if (slider != null) slider.value = lastSliderValue;
        if (bufferBar != null) bufferBar.fillAmount = lastSliderValue;
    }

    private IEnumerator BufferCo(float targetPercent)
    {
        float startFill = bufferBar.fillAmount;

        if (startFill > targetPercent)
        {
            yield return new WaitForSeconds(delayBeforeStart);

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                bufferBar.fillAmount = Mathf.Lerp(startFill, targetPercent, elapsed / transitionDuration);
                yield return null;
            }
        }

        bufferBar.fillAmount = targetPercent;
        bufferCo = null;
    }
}
