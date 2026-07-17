using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_SkillTree : MonoBehaviour
{
    [SerializeField] private UI_TreeConnectHandler[] parentNodes;
    public Player_SkillManager skillManager {  get; private set; }

    public bool EnoughSkillPoints(int cost)
    {
        if (SkillPointManager.Instance == null)
            return false;
        return SkillPointManager.Instance.AvailableSkillPoints >= cost;
    }
    
    public void RemoveSkillPoints(int cost)
    {
        SkillPointManager.Instance?.UseSkillPoints(cost);
    }
    
    public void AddSkillPoints(int points)
    {
        SkillPointManager.Instance?.RefundSkillPoints(points);
    }

    [SerializeField] private TextMeshProUGUI TextSkillCost;


    private void Awake()
    {
        skillManager = FindAnyObjectByType<Player_SkillManager>();
    }

    private void Start()
    {
        UpdateAllConnections();
        
        if (SkillPointManager.Instance != null)
        {
            SkillPointManager.Instance.OnSkillPointsChanged += UpdateSkillPointsDisplay;
        }
    }

    private void OnDestroy()
    {
        if (SkillPointManager.Instance != null)
        {
            SkillPointManager.Instance.OnSkillPointsChanged -= UpdateSkillPointsDisplay;
        }
    }

    private void UpdateSkillPointsDisplay(int availablePoints)
    {
        if (TextSkillCost != null)
        {
            TextSkillCost.text = "技能点： " + availablePoints;
        }
    }

    [ContextMenu("更新所有连线")]
    public void UpdateAllConnections()
    {
        foreach (var node in parentNodes)
        {
            node.UpdateAllConnections();
        }
    }


    [ContextMenu("重置所有技能")]
    public void RefundAllSkills()
    {
        // 1. 首先卸下技能槽上的所有技能
        if (SkillSlotManager.Instance != null)
        {
            int slotCount = SkillSlotManager.Instance.GetSlotCount();
            for (int i = 0; i < slotCount; i++)
            {
                SkillSlotManager.Instance.UnbindSkillFromSlot(i);
            }
            Debug.Log("已卸下所有技能槽上的技能");
        }

        // 2. 重置所有技能节点
        UI_TreeNode[] skillNodes = GetComponentsInChildren<UI_TreeNode>();

        foreach (var node in skillNodes)
            node.Refund();

        Debug.Log("所有技能已重置");
    }
}
