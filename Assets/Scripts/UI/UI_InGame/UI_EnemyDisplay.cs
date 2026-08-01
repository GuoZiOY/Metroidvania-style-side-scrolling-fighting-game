using System.Text;
using UnityEngine;
using TMPro;

public class UI_EnemyDisplay : UI_EntityDisplay
{
    [Header("名称和等级显示")]
    [SerializeField] private TextMeshProUGUI nameText; // 名称和等级文本组件
    [SerializeField] private bool showLevel = true; // 是否显示等级
    [SerializeField] private bool showType = true; // 是否显示敌人类型
    private string nameFormat = "{0}"; // 显示格式：{0}=名称
    private string levelFormat = "Lv.{0}"; // 等级格式

    [Header("类型颜色")]
    [SerializeField] private Color normalColor = Color.white; // 普通敌人颜色
    [SerializeField] private Color eliteColor = new Color(0.6f, 0.2f, 0.8f); // 精英敌人颜色（紫色）
    [SerializeField] private Color bossColor = Color.red; // Boss敌人颜色（红色）

    private Enemy enemy;

    protected override void Awake()
    {
        base.Awake();
        enemy = entity as Enemy;

        if (nameText == null)
        {
            nameText = GetComponentInChildren<TextMeshProUGUI>();
        }

    }

    private void Start()
    {
        if (nameText != null && enemy != null)
        {
            UpdateNameText();
        }
    }

    private void UpdateNameText()
    {
        if (nameText == null || enemy == null)
            return;

        int level = enemy.GetEnemyLevel();
        EnemyType enemyType = enemy.GetEnemyType();

        string typeText = GetTypeText(enemyType);
        Color typeColor = GetTypeColor(enemyType);

        string formattedName = string.Format(nameFormat, enemy.enemyName);
        string formattedLevel = showLevel ? string.Format(levelFormat, level) + " " : "";
        string formattedType = showType ? typeText + " " : "";

        // 附加词缀文本（富文本按 Tier 着色），精英/Boss 显示其激活词缀
        string affixText = BuildAffixText();

        nameText.text = formattedType + formattedLevel + formattedName + affixText;
        nameText.color = typeColor;
    }

    // 拼接敌人激活词缀的富文本：每个词缀以「」包裹并按 Tier 着色
    private string BuildAffixText()
    {
        if (enemy.ActiveAffixes == null || enemy.ActiveAffixes.Count == 0)
            return "";

        StringBuilder sb = new StringBuilder();
        foreach (var affix in enemy.ActiveAffixes)
        {
            if (affix == null)
                continue;

            string hex = ColorUtility.ToHtmlStringRGB(GetAffixColor(affix.Tier));
            sb.Append($" <color=#{hex}>「{affix.DisplayName}」</color>");
        }
        return sb.ToString();
    }

    // 词缀 Tier → 颜色（普通白 / 稀有金 / 传说红）
    private Color GetAffixColor(AffixTier tier)
    {
        return tier switch
        {
            AffixTier.Common => Color.white,
            AffixTier.Rare => new Color(1f, 0.84f, 0f),   // 金色
            AffixTier.Legendary => new Color(1f, 0.3f, 0.3f), // 红色
            _ => Color.white
        };
    }

    private string GetTypeText(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Normal:
                return "";
            case EnemyType.Elite:
                return "[精英]";
            case EnemyType.Boss:
                return "[BOSS]";
            default:
                return "";
        }
    }

    private Color GetTypeColor(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Normal:
                return normalColor;
            case EnemyType.Elite:
                return eliteColor;
            case EnemyType.Boss:
                return bossColor;
            default:
                return normalColor;
        }
    }
}
