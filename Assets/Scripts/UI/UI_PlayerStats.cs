using UnityEngine;

public class UI_PlayerStats : MonoBehaviour
{
    private UI_StatSlot[] UI_statSlots;
    private Inventory_Player inventory;

    private void Awake()
    {
        UI_statSlots = GetComponentsInChildren<UI_StatSlot>();

        inventory = FindAnyObjectByType<Inventory_Player>();
        inventory.OnInventoryUpdated += UpdateStatsUI;

        if (AttributePointManager.Instance != null)
            AttributePointManager.Instance.OnAttributesChanged += UpdateStatsUI;

        if (SkillDataManager.Instance != null)
            SkillDataManager.Instance.OnSkillDataUpdated += OnSkillDataUpdated;
    }

    private void OnDestroy()
    {
        if (SkillDataManager.Instance != null)
            SkillDataManager.Instance.OnSkillDataUpdated -= OnSkillDataUpdated;
    }

    private void Start()
    {
        UpdateStatsUI();
    }

    private void OnSkillDataUpdated(SkillUpgradeType upgradeType, int newLevel)
    {
        UpdateStatsUI();
    }

    private void UpdateStatsUI()
    {
        foreach (var statSlot in UI_statSlots)
            statSlot.UpdateStateValue();
    }
}
