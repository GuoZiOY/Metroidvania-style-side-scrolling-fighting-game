using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillSlotManager : MonoBehaviour
{
    public static SkillSlotManager Instance { get; private set; }

    [Header("技能槽位配置")]
    [SerializeField] private int slotCount = 5; // 技能槽位数量

    private List<SkillSlotData> skillSlots; // 技能槽位列表
    private Player_SkillManager skillManager; // 技能管理器引用

    public event Action<int, SkillUpgradeType> OnSkillSlotChanged; // 技能槽位改变事件

    private void Awake() // 初始化单例
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeSkillSlots();
    }

    private void Start() // 获取技能管理器引用
    {
        skillManager = Player_SkillManager.Instance ?? FindAnyObjectByType<Player_SkillManager>();

        // 监听数据更新事件
        if (SkillDataManager.Instance != null)
        {
            SkillDataManager.Instance.OnSkillDataUpdated += OnSkillDataUpdated;
        }
    }

    private void OnDestroy() // 清理事件监听
    {
        if (SkillDataManager.Instance != null)
        {
            SkillDataManager.Instance.OnSkillDataUpdated -= OnSkillDataUpdated;
        }
    }

    private void OnSkillDataUpdated(SkillUpgradeType upgradeType, int newLevel) // 技能数据更新事件处理
    {
        // 检查是否为被动技能，如果是则跳过处理
        Skill_DataSo skillData = SkillDataManager.Instance?.GetSkillData(upgradeType);
        if (skillData != null && skillData.usageType == SkillUsageType.Passive)
        {
            return;
        }

        // 查找装备了该技能的槽位
        int slotIndex = GetSlotIndexByUpgradeType(upgradeType);
        if (slotIndex >= 0)
        {
            // 重新激活技能，应用新的等级数据
            ActivateUpgradeType(upgradeType);
        }
    }

    private void Update() // 每帧更新
    {
        HandleSkillSlotInput();
    }

    private void InitializeSkillSlots() // 初始化技能槽位
    {
        skillSlots = new List<SkillSlotData>();
        for (int i = 0; i < slotCount; i++)
        {
            skillSlots.Add(new SkillSlotData(i));
        }
    }

    private static KeyCode GetSlotKey(int slotIndex)
    {
        return slotIndex switch
        {
            0 => GameInput.GetBinding(GameInput.Action.SkillSlot1),
            1 => GameInput.GetBinding(GameInput.Action.SkillSlot2),
            2 => GameInput.GetBinding(GameInput.Action.SkillSlot3),
            3 => GameInput.GetBinding(GameInput.Action.SkillSlot4),
            4 => GameInput.GetBinding(GameInput.Action.SkillSlot5),
            _ => KeyCode.None,
        };
    }

    private void HandleSkillSlotInput() // 处理技能槽位输入
    {
        if (skillManager == null) return;

        for (int i = 0; i < skillSlots.Count; i++)
        {
            if (Input.GetKeyDown(GetSlotKey(i)))
                TryUseSkillInSlot(skillSlots[i].slotIndex);
        }
    }

    private bool IsValidSlotIndex(int slotIndex) => slotIndex >= 0 && slotIndex < skillSlots.Count; // 检查槽位索引是否有效

    public void TryUseSkillInSlot(int slotIndex) // 尝试使用指定槽位的技能
    {
        if (!IsValidSlotIndex(slotIndex)) return;

        SkillSlotData slot = skillSlots[slotIndex];
        if (!slot.isOccupied) return;

        SkillType skillType = GetSkillTypeByUpgradeType(slot.upgradeType);
        if (skillType == SkillType.None) return;

        Skill_Base skill = skillManager.GetSkillByType(skillType);
        skill?.TryUseSkill();
    }

    public bool BindSkillToSlot(SkillUpgradeType upgradeType, int slotIndex) // 绑定技能到指定槽位
    {
        if (!IsValidSlotIndex(slotIndex) || upgradeType == SkillUpgradeType.None) return false;

        skillSlots[slotIndex].BindSkill(upgradeType);
        ActivateUpgradeType(upgradeType);
        OnSkillSlotChanged?.Invoke(slotIndex, upgradeType);
        return true;
    }

    public bool UnbindSkillFromSlot(int slotIndex) // 解绑指定槽位的技能
    {
        if (!IsValidSlotIndex(slotIndex)) return false;

        SkillUpgradeType upgradeType = skillSlots[slotIndex].upgradeType;
        skillSlots[slotIndex].UnbindSkill();

        if (upgradeType != SkillUpgradeType.None)
            DeactivateUpgradeType(upgradeType);

        OnSkillSlotChanged?.Invoke(slotIndex, SkillUpgradeType.None);
        return true;
    }

    public void SwapSkills(int slotIndex1, int slotIndex2) // 交换两个槽位的技能
    {
        if (!IsValidSlotIndex(slotIndex1) || !IsValidSlotIndex(slotIndex2)) return;

        SkillUpgradeType upgradeType1 = skillSlots[slotIndex1].upgradeType;
        SkillUpgradeType upgradeType2 = skillSlots[slotIndex2].upgradeType;

        skillSlots[slotIndex1].BindSkill(upgradeType2);
        skillSlots[slotIndex2].BindSkill(upgradeType1);

        if (upgradeType2 != SkillUpgradeType.None) ActivateUpgradeType(upgradeType2);
        if (upgradeType1 != SkillUpgradeType.None) ActivateUpgradeType(upgradeType1);

        OnSkillSlotChanged?.Invoke(slotIndex1, upgradeType2);
        OnSkillSlotChanged?.Invoke(slotIndex2, upgradeType1);
    }

    public SkillUpgradeType GetUpgradeTypeInSlot(int slotIndex) // 获取指定槽位的技能升阶类型
    {
        return IsValidSlotIndex(slotIndex) ? skillSlots[slotIndex].upgradeType : SkillUpgradeType.None;
    }

    public int GetSlotCount() => skillSlots.Count; // 获取技能槽位数量

    public bool IsUpgradeTypeBound(SkillUpgradeType upgradeType) // 检查技能升阶类型是否已绑定到任意槽位
    {
        foreach (var slot in skillSlots)
        {
            if (slot.upgradeType == upgradeType) return true;
        }
        return false;
    }

    public int GetSlotIndexByUpgradeType(SkillUpgradeType upgradeType) // 根据技能升阶类型获取槽位索引
    {
        for (int i = 0; i < skillSlots.Count; i++)
        {
            if (skillSlots[i].upgradeType == upgradeType) return i;
        }
        return -1;
    }

    public int GetSlotIndexBySkillType(SkillType skillType) // 根据技能大类型获取槽位索引
    {
        for (int i = 0; i < skillSlots.Count; i++)
        {
            if (skillSlots[i].upgradeType != SkillUpgradeType.None)
            {
                SkillType slotSkillType = GetSkillTypeByUpgradeType(skillSlots[i].upgradeType);
                if (slotSkillType == skillType) return i;
            }
        }
        return -1;
    }

    private SkillType GetSkillTypeByUpgradeType(SkillUpgradeType upgradeType) // 从DataManager获取技能大类型
    {
        if (upgradeType == SkillUpgradeType.None) return SkillType.None;
        return SkillDataManager.Instance?.GetSkillType(upgradeType) ?? SkillType.None;
    }

    private void ActivateUpgradeType(SkillUpgradeType upgradeType) // 激活指定的技能升阶类型
    {
        if (upgradeType == SkillUpgradeType.None) return;

        SkillType skillType = SkillDataManager.Instance?.GetSkillType(upgradeType) ?? SkillType.None;
        if (skillType == SkillType.None) return;

        int level = SkillDataManager.Instance?.GetCurrentLevel(upgradeType) ?? 0;
        if (level <= 0) return;

        LevelData levelData = SkillDataManager.Instance?.GetLevelData(upgradeType, level);
        if (levelData == null) return;

        Skill_Base skill = skillManager.GetSkillByType(skillType);
        skill?.SetSkillLevelData(upgradeType, levelData, level, true);
    }

    private void DeactivateUpgradeType(SkillUpgradeType upgradeType) // 停用指定的技能升阶类型
    {
        if (upgradeType == SkillUpgradeType.None) return;

        SkillType skillType = SkillDataManager.Instance?.GetSkillType(upgradeType) ?? SkillType.None;
        if (skillType == SkillType.None) return;

        Skill_Base skill = skillManager.GetSkillByType(skillType);
        if (skill == null) return;

        skill.upgradeType = SkillUpgradeType.None;
    }
}