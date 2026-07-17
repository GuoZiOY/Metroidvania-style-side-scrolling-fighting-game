using System;
using UnityEngine;

public class SkillPointManager : MonoBehaviour
{
    public static SkillPointManager Instance { get; private set; }

    [Header("技能点统计")]
    [SerializeField] private int totalSkillPoints = 0;
    [SerializeField] private int usedSkillPoints = 0;

    public int AvailableSkillPoints => totalSkillPoints - usedSkillPoints;
    public int TotalSkillPoints => totalSkillPoints;
    public int UsedSkillPoints => usedSkillPoints;

    public event Action<int> OnSkillPointsChanged;
    public event Action<int> OnSkillPointsUsed;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddSkillPoints(int amount, string source = "未知来源")
    {
        if (amount <= 0)
            return;

        totalSkillPoints += amount;
        Debug.Log($"技能点增加 +{amount}，来源：{source}，当前总技能点：{totalSkillPoints}");
        OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
    }

    public bool UseSkillPoints(int amount)
    {
        if (amount <= 0)
            return false;

        if (AvailableSkillPoints < amount)
        {
            Debug.LogWarning($"技能点不足！需要 {amount} 点，可用 {AvailableSkillPoints} 点");
            return false;
        }

        usedSkillPoints += amount;
        Debug.Log($"技能点消耗 -{amount}，已用：{usedSkillPoints}，剩余可用：{AvailableSkillPoints}");
        OnSkillPointsUsed?.Invoke(AvailableSkillPoints);
        OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
        return true;
    }

    public void RefundSkillPoints(int amount)
    {
        if (amount <= 0)
            return;

        usedSkillPoints = Mathf.Max(0, usedSkillPoints - amount);
        Debug.Log($"技能点返还 +{amount}，已用：{usedSkillPoints}，剩余可用：{AvailableSkillPoints}");
        OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
    }

    public void ResetSkillPoints()
    {
        int refundedPoints = usedSkillPoints;
        usedSkillPoints = 0;
        Debug.Log($"技能点重置，返还 {refundedPoints} 点，当前可用：{AvailableSkillPoints}");
        OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
    }

    public void LoadFromSave(int total, int used)
    {
        totalSkillPoints = total;
        usedSkillPoints = used;
        OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
    }

    public void SetTotalSkillPoints(int amount)
    {
        if (amount < 0)
            return;

        int difference = amount - totalSkillPoints;
        totalSkillPoints = amount;
        
        if (difference != 0)
        {
            Debug.Log($"总技能点设置为 {amount}，变化：{difference:+0;-0}");
            OnSkillPointsChanged?.Invoke(AvailableSkillPoints);
        }
    }
}
