using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_InGame : MonoBehaviour
{
    [SerializeField] private RectTransform healthRect;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [SerializeField] private Slider expSlider; // 经验条滑动条
    [SerializeField] private TextMeshProUGUI expText; // 经验值文本显示

    private Player player;

    void Start()
    {
        player = FindAnyObjectByType<Player>();
        player.health.OnHealthUpdate += UpdateHealthBar;

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnExpGained += UpdateExpBar;
            PlayerLevelManager.Instance.OnLevelUp += UpdateExpBar;
            UpdateExpBar(0); // 初始化经验条显示
        }
    }

    private void UpdateHealthBar()
    {
        float cuarrentHP = Mathf.RoundToInt(player.health.GetCurrentHP());
        float maxHP = player.stats.GetMaxHP();

        healthText.text = cuarrentHP + "/" + maxHP;
        healthSlider.value = player.health.GetHealthPercent();
    }

    private void UpdateExpBar(int expAmount)
    {
        if (PlayerLevelManager.Instance == null)
            return;

        int currentExp = PlayerLevelManager.Instance.CurrentExp;
        int expToNextLevel = PlayerLevelManager.Instance.ExpToNextLevel;
        int currentLevel = PlayerLevelManager.Instance.CurrentLevel;
        float expProgress = PlayerLevelManager.Instance.ExpProgress;

        if (expText != null)
        {
            expText.text = $"Lv.{currentLevel} {currentExp}/{expToNextLevel}";
        }

        if (expSlider != null)
        {
            expSlider.value = expProgress;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.health.OnHealthUpdate -= UpdateHealthBar;
        }

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnExpGained -= UpdateExpBar;
            PlayerLevelManager.Instance.OnLevelUp -= UpdateExpBar;
        }
    }
}
