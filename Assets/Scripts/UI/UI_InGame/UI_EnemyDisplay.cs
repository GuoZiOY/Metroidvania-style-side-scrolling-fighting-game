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

        nameText.text = formattedType + formattedLevel + formattedName;
        nameText.color = typeColor;
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
