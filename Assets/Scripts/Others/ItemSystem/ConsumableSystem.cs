using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;



public class ConsumableSystem : MonoBehaviour
{
    private Entity_Stats playerStats;//玩家属性引用
    private Entity_Health playerHealth;//玩家生命值引用
    private Inventory_Base inventory;//背包引用

    private Dictionary<string, float> consumableCooldowns = new Dictionary<string, float>();//消耗品冷却时间字典

    public event Action OnConsumableUsed;//消耗品使用事件

    private void Awake()
    {
        playerStats = GetComponentInParent<Entity_Stats>();
        playerHealth = GetComponentInParent<Entity_Health>();
        inventory = GetComponentInParent<Inventory_Base>();
    }

    private void Update()//更新消耗品冷却时间
    {
        UpdateConsumableCooldowns();
    }

    public void ApplyConsumableEffect(ConsumableDataSo consumableData)
    {
        if (consumableData == null)
        {
            Debug.LogError("消耗品数据为空");
            return;
        }

        switch (consumableData.effectType)
        {
            case ConsumableEffectType.恢复生命:
                RestoreHealth(consumableData.effectValue);
                break;

            case ConsumableEffectType.恢复生命百分比:
                RestoreHealthPercentage(consumableData.effectValue);
                break;

            case ConsumableEffectType.属性增益:
                ApplyBuff(consumableData.buffStatType, consumableData.effectValue, consumableData.effectDuration);
                break;

            case ConsumableEffectType.复活:
                RevivePlayer();
                break;

            default:
                Debug.LogWarning($"未实现的消耗品效果类型: {consumableData.effectType}");
                break;
        }
    }

    private void RestoreHealth(float amount)//恢复指定数量的生命值
    {
        if (playerHealth != null)
        {
            playerHealth.IncreaseHP(amount);
        }
    }

    private void RestoreHealthPercentage(float percentage)//恢复指定百分比的生命值
    {
        if (playerHealth != null && playerStats != null)
        {
            float maxHP = playerStats.GetMaxHP();
            float healAmount = maxHP * (percentage / 100f);
            playerHealth.IncreaseHP(healAmount);
        }
    }

    private void ApplyBuff(StatType statType, float value, float duration)//应用属性增益
    {
        if (playerStats == null)
            return;

        Stat statToModify = playerStats.GetStatByType(statType);
        if (statToModify != null)
        {
            string buffID = "ConsumableBuff_" + System.Guid.NewGuid().ToString().Substring(0, 8);
            statToModify.AddModifier(value, buffID);

            if (duration > 0)
            {
                StartCoroutine(RemoveBuffAfterDuration(statToModify, buffID, duration));
            }
        }
    }

    private IEnumerator RemoveBuffAfterDuration(Stat stat, string buffID, float duration)//在持续时间结束后移除增益
    {
        yield return new WaitForSeconds(duration);
        stat.RemoveModifier(buffID);
        if (inventory != null)
        {
            inventory.TriggerInventoryUpdate();
        }
    }

    private void RevivePlayer()//复活玩家
    {
        if (playerHealth != null && playerStats != null)
        {
            float reviveHP = playerStats.GetMaxHP() * 0.5f;//复活时恢复50%生命值
            playerHealth.IncreaseHP(reviveHP);
            Debug.Log("玩家已复活，恢复生命值: " + reviveHP);
        }
    }

    public bool TryUseConsumable(Inventory_Item item)//尝试使用消耗品
    {
        if (item == null)
            return false;

        if (!item.IsConsumable)
        {
            Debug.LogWarning("该物品不是消耗品");
            return false;
        }

        if (!item.CanUseConsumable())
        {
            Debug.LogWarning("该消耗品无法使用");
            return false;
        }

        ConsumableDataSo consumableData = item.GetConsumableData();
        if (consumableData == null)
            return false;

        string cooldownKey = GetCooldownKey(consumableData);

        if (IsConsumableOnCooldown(cooldownKey))
        {
            Debug.LogWarning($"消耗品 {consumableData.itemName} 正在冷却中，冷却时间：{GetConsumableCooldownRemaining(cooldownKey)}");
            return false;
        }

        ApplyConsumableEffect(consumableData);

        item.currentStackSize--;

        if (item.currentStackSize <= 0)
        {
            inventory.RemoveItem(item);
        }
        else
        {
            inventory.TriggerInventoryUpdate();
        }

        if (consumableData.cooldownTime > 0)
        {
            SetConsumableCooldown(cooldownKey, consumableData.cooldownTime);
        }

        OnConsumableUsed?.Invoke();
        return true;
    }

    private string GetCooldownKey(ConsumableDataSo consumableData)//获取冷却键
    {
        if (consumableData.consumableType == ConsumableType.特殊)
        {
            return consumableData.itemName;
        }
        else
        {
            return consumableData.consumableType.ToString();
        }
    }

    private bool IsConsumableOnCooldown(string itemName)//检查消耗品是否在冷却中
    {
        if (consumableCooldowns.ContainsKey(itemName))
        {
            return consumableCooldowns[itemName] > 0;
        }
        return false;
    }

    private void SetConsumableCooldown(string itemName, int cooldownTime)//设置消耗品冷却时间
    {
        consumableCooldowns[itemName] = cooldownTime;
    }

    private void UpdateConsumableCooldowns()//更新所有消耗品的冷却时间
    {
        List<string> keysToRemove = null;

        foreach (var key in new List<string>(consumableCooldowns.Keys))
        {
            float newCooldown = consumableCooldowns[key] - Time.deltaTime;
            
            if (newCooldown <= 0)
            {
                if (keysToRemove == null)
                    keysToRemove = new List<string>();
                keysToRemove.Add(key);
            }
            else
            {
                consumableCooldowns[key] = newCooldown;
            }
        }

        if (keysToRemove != null)
        {
            foreach (var key in keysToRemove)
            {
                consumableCooldowns.Remove(key);
            }
        }
    }

    public float GetConsumableCooldownRemaining(string itemName)//获取消耗品剩余冷却时间
    {
        if (consumableCooldowns.ContainsKey(itemName))
        {
            return Mathf.Max(0, consumableCooldowns[itemName]);
        }
        return 0;
    }
}
