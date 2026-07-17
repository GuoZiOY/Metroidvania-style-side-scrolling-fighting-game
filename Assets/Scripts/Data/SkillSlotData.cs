using System;
using UnityEngine;

[Serializable]
public class SkillSlotData
{
    public SkillUpgradeType upgradeType; // 槽位绑定的技能升阶类型
    public int slotIndex; // 槽位索引（0-4）
    public bool isOccupied => upgradeType != SkillUpgradeType.None; // 槽位是否被占用

    public SkillSlotData(int index) // 构造函数：初始化技能槽位数据
    {
        slotIndex = index; // 设置槽位索引
        upgradeType = SkillUpgradeType.None; // 初始化为无技能
    }

    public void BindSkill(SkillUpgradeType type) // 绑定技能到槽位
    {
        upgradeType = type; // 设置技能升阶类型
    }

    public void UnbindSkill() // 解绑槽位的技能
    {
        upgradeType = SkillUpgradeType.None; // 重置为无技能
    }
}
