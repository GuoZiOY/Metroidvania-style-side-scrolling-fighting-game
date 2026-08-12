using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss 顶部大血条（公共）——绑定任意 Boss 的 Entity_Health，显示名称+血量填充
// Canvas 结构由 UI 手动搭建（名称 Text + Fill Image[type=Filled] + CanvasGroup），挂在持久 HUD 层级
public class UI_BossHealthBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;   // Boss 名称文本
    [SerializeField] private Image fillImage;             // 血条填充（Image.type = Filled, fillMethod = Horizontal）
    [SerializeField] private CanvasGroup canvasGroup;     // 整条显隐/淡入控制

    private Entity_Health boundHealth; // 当前绑定的 Boss 生命

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
        OnHealthUpdated(); // 立即刷新（100%）
    }

    // 解绑：Boss 死亡（OnEntityDead）时调用，防销毁后迟到回调 MissingReferenceException
    public void Unbind()
    {
        if (boundHealth != null)
            boundHealth.OnHealthUpdate -= OnHealthUpdated;
        boundHealth = null;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnHealthUpdated()
    {
        if (boundHealth == null || fillImage == null)
            return;
        fillImage.fillAmount = boundHealth.GetHealthPercent();
    }
}
