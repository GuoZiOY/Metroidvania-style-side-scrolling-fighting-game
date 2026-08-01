using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_TreeNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private UI ui;
    private RectTransform rect;
    private UI_SkillTree skillTree;
    private UI_TreeConnectHandler connectHandler;
    private CanvasGroup canvasGroup;

    [Header("节点状态")]
    public bool isUnlocked;//已解锁
    public bool isLocked;//已锁定
    private List<UI_TreeNode> requiredPreNodes = new List<UI_TreeNode>();
    private List<UI_TreeNode> conflictingNodes = new List<UI_TreeNode>();

    [Header("技能数据")]
    public Skill_DataSo skillData;
    [SerializeField] private float skillCost;
    [SerializeField] private string skillName;//技能名称
    [SerializeField] private Image skillIcom;//技能图标
    [SerializeField] private string lockedColorHex = "#646464";
    [SerializeField] private float scaleMultiplier = 1.1f; // 缩放倍率，1.1表示放大10%
    private Color lastColor;
    private Vector3 originalScale; // 初始缩放比例

    [Header("等级系统")]
    [SerializeField] private Button levelUpButton; // 升级"+"按钮
    public int CurrentLevel; // 当前技能等级
    public int maxLevel => skillData ? skillData.maxLevel : 0; // 最大等级从技能数据获取


    private void Awake()
    {
        ui = GetComponentInParent<UI>();
        rect = GetComponent<RectTransform>();
        skillTree = GetComponentInParent<UI_SkillTree>();
        connectHandler = GetComponent<UI_TreeConnectHandler>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        UpdateIconColor(GetColorByHex(lockedColorHex));
        // 记录初始缩放比例（防御：读档时序中 LoadStateFromSave 已把图标缩放到 0，
        // 若当前缩放为 0 则回退正常尺寸，避免 originalScale 永远为 0 导致图标消失）
        originalScale = skillIcom.rectTransform.localScale;
        if (originalScale.sqrMagnitude <= 0.0001f)
            originalScale = Vector3.one;


        InitConflictingNodes();//初始化冲突节点

        // 读档时序防御：技能树面板 inactive 时 Awake 延迟执行，读档已先调用 LoadStateFromSave
        // 设置白色图标；本 Awake 若重置为锁定灰色会覆盖还原状态。管理器已有等级则恢复已解锁视觉。
        // 注意：这里只做图标/连线等单节点视觉恢复，不调用 LockConflictNodes —— 冲突锁定会访问
        // 其他节点的 connectHandler，而 Awake 跨节点执行顺序未定，此时其字段可能尚未初始化
        if (skillData != null && SkillDataManager.Instance != null)
        {
            int restoredLevel = SkillDataManager.Instance.GetCurrentLevel(skillData.upgradeType);
            if (restoredLevel > 0)
            {
                isUnlocked = true;
                CurrentLevel = restoredLevel;
                UpdateIconColor(Color.white);
                connectHandler?.UnlockConnectionImage(true);
                SkillIconScale(true); // 恢复图标为已解锁放大尺寸（SkillIconScale 内部已防御零缩放）
            }
        }

        if (levelUpButton != null)
        {
            // 绑定按钮点击事件
            levelUpButton.onClick.AddListener(OnLevelUpButtonClicked);
            // 初始状态，按钮不可用（未解锁）
            levelUpButton.interactable = false;
        }

    }

    // 供外部调用获取前置节点列表（用于UI显示）
    public UI_TreeNode[] GetRequiredPreNodes()
    {
        CheckAllPreSkillRequirements();
        // 如果列表为空，返回空数组（而不是null）
        return requiredPreNodes.Count == 0 ? new UI_TreeNode[0] : requiredPreNodes.ToArray();
    }

    // 供外部调用获取冲突节点列表（用于UI显示）
    public UI_TreeNode[] GetConflictingNodes()
    {
        // 如果列表为空，返回空数组（而不是null）
        return conflictingNodes.Count == 0 ? new UI_TreeNode[0] : conflictingNodes.ToArray();
    }



    // 存档读档后刷新节点状态（完整复制 UnLock + 升级的视觉效果）
    public void LoadStateFromSave(int level)
    {
        if (skillData == null || level <= 0) return;
        isUnlocked = true;
        CurrentLevel = level;
        UpdateIconColor(Color.white);
        connectHandler?.UnlockConnectionImage(true);
        SkillIconScale(true);
        LockConflictNodes();
        ApplySkillLevelData(level);
        UpdateLevelUpButtonState();
        UpdateSkillToolTipData();
    }

    private void Update()
    {
        if (isUnlocked) UpdateLevelUpButtonState();// 实时更新升级按钮状态
    }



    // 处理UI_TreeNode上的升级按钮点击事件
    private void OnLevelUpButtonClicked()
    {
        int upgradeCost = skillData.levelDatas[CurrentLevel].levelUpCost;
        if (!skillTree.EnoughSkillPoints(upgradeCost))
        {
            AudioManager.Instance?.PlayDenySfx();
            ui.skillToolTip.NotEnoughSkillPointsEffect();
            ui.skillTip.ShowNotEnoughSkillPointsTip();
            return;
        }

        // 3. 技能点充足时执行升级（之前CanLevelUp已确认，这里确保逻辑正确）
        if (CanLevelUp())
        {
            skillTree.RemoveSkillPoints(upgradeCost);
            CurrentLevel++;
            ApplySkillLevelData(CurrentLevel);
            UpdateLevelUpButtonState();
            UpdateSkillToolTipData();
            ui.skillTip.ShowUpgradeSuccess(skillData.displayName, CurrentLevel);

                // 升级弹性动画（先复位再弹，防连续点击累积偏移）
            skillIcom.rectTransform.DOKill();
            skillIcom.rectTransform.localScale = originalScale * scaleMultiplier;
            skillIcom.rectTransform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 3, 0.5f);
        }
    }

    public bool CanLevelUp()
    {
        if (!isUnlocked) return false;
        if (CurrentLevel >= maxLevel) return false;
        if (skillData.levelDatas.Length <= CurrentLevel) return false;
        return true;
    }

    private void ApplySkillLevelData(int level)//应用技能等级数据
    {
        // 技能树只管理自己的数据，并同步到DataManager
        CurrentLevel = level; // 更新当前等级

        // 同步数据到DataManager
        SyncDataToDataManager();
    }

    private void SyncDataToDataManager() // 同步数据到DataManager
    {
        if (skillData == null || skillData.upgradeType == SkillUpgradeType.None)
            return;

        // 确保skillData的状态与节点状态一致
        skillData.isUnlocked = isUnlocked;
        skillData.currentLevel = CurrentLevel;

        // 同步技能数据到DataManager
        SkillDataManager.Instance?.UpdateSkillData(skillData.upgradeType, skillData, CurrentLevel);
    }

    private void UpdateLevelUpButtonState()
    {
        if (levelUpButton == null) return;
        // 只有已解锁且未满级时按钮可用
        levelUpButton.interactable = CanLevelUp();
    }

    public void Refund()//退款
    {
        //计算总退款：基础+升级消耗
        int totalRefund = 0;
        for (int i = 0; i < CurrentLevel; i++) totalRefund += skillData.levelDatas[i].levelUpCost;
        skillTree.AddSkillPoints(totalRefund);

        // 重置状态
        isUnlocked = false;
        isLocked = false;
        CurrentLevel = 0; // 等级重置为0
        UpdateIconColor(GetColorByHex(lockedColorHex));

        // 3. 关闭连线、恢复图标（原逻辑）
        connectHandler?.UnlockConnectionImage(false);
        SkillIconScale(false);

        // 4. 同步数据到DataManager（不再直接操作技能系统）
        SyncDataToDataManager();
    }

    public void UnLock()
    {
        // 前置条件校验（防止重复解锁）
        if (isUnlocked || isLocked || !skillTree.EnoughSkillPoints(skillData.levelDatas[0].levelUpCost))
            return;

        // 更新解锁状态
        isUnlocked = true;
        CurrentLevel = 1;
        UpdateIconColor(Color.white);

        // 锁定冲突节点
        LockConflictNodes();

        // 扣除技能点
        skillTree.RemoveSkillPoints(skillData.levelDatas[0].levelUpCost);

        // 开启连线、放大图标
        connectHandler?.UnlockConnectionImage(true);
        SkillIconScale(true);

        // 解锁弹性动画（先复位至目标大小再弹，防连续点击累积偏移）
        skillIcom.rectTransform.DOKill();
        skillIcom.rectTransform.localScale = originalScale * scaleMultiplier;
        skillIcom.rectTransform.DOPunchScale(Vector3.one * 0.4f, 0.4f, 5, 0.5f);

        // 应用技能数据
        ApplySkillLevelData(CurrentLevel);
        UpdateLevelUpButtonState();
        UpdateSkillToolTipData();
        ui.skillTip.ShowUnlockSuccess(skillData.displayName);
    }

    private void UpdateSkillToolTipData()
    {
        if (ui != null && ui.skillToolTip != null) ui.skillToolTip.RefreshToolTip();
    }

    public bool CanBeUnLocked()//可以解锁
    {
        if (isLocked || isUnlocked)
            return false;

        if (skillTree.EnoughSkillPoints(skillData.levelDatas[0].levelUpCost) == false)
        {
            AudioManager.Instance?.PlayDenySfx();
            ui.skillToolTip.NotEnoughSkillPointsEffect();
            ui.skillTip.ShowNotEnoughSkillPointsTip();
            return false;
        }


        //校验冲突节点
        foreach (var node in conflictingNodes)
        {
            if (node.isUnlocked) return false;
        }

        // 前置技能达到指定等级，校验前置要求
        if (!CheckAllPreSkillRequirements())
        {
            AudioManager.Instance?.PlayDenySfx();
            ui.skillTip.ShowPreSkillNotMetTip();
            return false;
        }

        return true;
    }


    // 校验当前技能的前置要求全部满足才返回true
    private bool CheckAllPreSkillRequirements()
    {
        requiredPreNodes.Clear(); // 每次校验前清空列表

        // 前置要求  直接通过
        if (skillData.preSkillRequirements == null || skillData.preSkillRequirements.Length == 0)
            return true;

        // 遍历当前技能的前置要求，动态查找节点并校验
        foreach (var req in skillData.preSkillRequirements)
        {
            UI_TreeNode preNode = FindSkillNodeByType(req.preSkillType);
            if (preNode == null) return false; // 未找到前置节点  校验失败

            requiredPreNodes.Add(preNode); // 添加前置节点列表

            // 校验：前置节点已解锁 + 等级达标
            if (!preNode.isUnlocked || preNode.CurrentLevel < req.preSkillLevel)
            {
                Debug.Log($"前置技能 {req.preSkillType} 未完成（需Lv.{req.preSkillLevel}，当前Lv.{preNode.CurrentLevel}）");
                return false;
            }
        }
        return true;
    }

    public UI_TreeNode FindSkillNodeByType(SkillUpgradeType targetType)//全局查找技能节点（支持跨技能树）
    {
        // 从技能树获取所有子节点（包括非激活节点）
        UI_TreeNode[] allSkillNodes = skillTree.GetComponentsInChildren<UI_TreeNode>(true); // true=包括非激活节点
        foreach (var node in allSkillNodes)
        {
            if (node.skillData != null && node.skillData.upgradeType == targetType)
            {
                return node;
            }
        }
        return null;
    }

    // 初始化冲突节点（从skillData获取类型，动态查找）
    private void InitConflictingNodes()
    {
        conflictingNodes.Clear();
        if (skillData.conflictSkillTypes == null || skillData.conflictSkillTypes.Length == 0)
            return;

        // 遍历冲突技能类型，动态查找对应节点
        foreach (var conflictType in skillData.conflictSkillTypes)
        {
            UI_TreeNode conflictNode = FindSkillNodeByType(conflictType);
            if (conflictNode != null)
            {
                conflictingNodes.Add(conflictNode);
            }
        }
    }


    private void LockConflictNodes()//锁定冲突技能的节点（递归）
    {
        foreach (var node in conflictingNodes)
        {
            node.isLocked = true;
            node.LockChildNodes();//节点递归锁定
        }
    }


    public void LockChildNodes()//递归锁定子节点
    {
        isLocked = true;//锁定自己

        // 防御：connectHandler 可能为 null（节点未挂 UI_TreeConnectHandler，或 Awake 顺序未初始化），
        // 直接跳过子节点递归，避免 NRE
        if (connectHandler == null)
            return;

        foreach (var node in connectHandler.GetChildNodes())
            node.LockChildNodes();//递归锁定自己及其子节点
    }

    public void OnPointerDown(PointerEventData eventData)//点击
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            AudioManager.Instance?.PlayButtonSfx();

            if (CanBeUnLocked())
                UnLock();
            else if (isLocked)
            {
                AudioManager.Instance?.PlayDenySfx();
                ui.skillToolTip.LockedSkillEffect();
                ui.skillTip.ShowConflictLockedTip();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)//鼠标进入
    {
        // 检查鼠标是否在升级按钮上
        if (levelUpButton != null && RectTransformUtility.RectangleContainsScreenPoint(
            levelUpButton.GetComponent<RectTransform>(), 
            eventData.position, 
            eventData.pressEventCamera))
        {
            return; // 如果鼠标在升级按钮上，不显示提示框
        }

        ui.skillToolTip.ShowToolTip(true, rect,this);

        if (isUnlocked || isLocked)
            return;

        ToggleNodeHighlight(true);//颜色高亮
        SkillIconScale(true);// 鼠标进入时放大图标

    }

    public void OnPointerExit(PointerEventData eventData)//鼠标退出
    {
        ui.skillToolTip.ShowToolTip(false, rect);

        if (isUnlocked || isLocked)
            return;
        
         ToggleNodeHighlight(false);//恢复原始颜色（非高亮）
         SkillIconScale(false);// 鼠标退出时恢复原始大小
       


    }

    public void SkillIconScale(bool isBig)
    {
        // 悬停/取消悬停时图标平滑缩放（DOTween 替代原硬切）
        // 防御：节点 Awake 未执行（技能树 inactive）时 originalScale 为默认 (0,0,0)，
        // 直接乘会缩放归零导致图标消失；退化为正常尺寸
        Vector3 baseScale = originalScale.sqrMagnitude > 0.0001f ? originalScale : Vector3.one;
        Vector3 target = isBig ? baseScale * scaleMultiplier : baseScale;
        skillIcom.rectTransform.DOScale(target, 0.12f).SetEase(Ease.OutQuad);
    }

    private void ToggleNodeHighlight(bool highlight)
    {
        Color highlightColor = Color.white * 0.8f; highlightColor.a = 1;
        Color colorToApply = highlight ? highlightColor : lastColor;//高亮时返回高亮颜色，否则返回原始颜色

        UpdateIconColor(colorToApply);

    }



    public void UpdateIconColor(Color color)//更新图标颜色
    {
        if (skillIcom == null)
            return;

        lastColor = skillIcom.color;
        skillIcom.color = color;
    }

    private Color GetColorByHex(string hexNumber)//将颜色的十六进制字符串转换为颜色
    {
        ColorUtility.TryParseHtmlString(hexNumber, out Color color);
        return color;
    }

    private void OnValidate()//编辑器修改时
    {
        if (skillData == null)
            return;

        skillName = skillData.displayName;
        skillIcom.sprite = skillData.icon;
        gameObject.name = "UI_TreeNode -" + skillData.displayName;
    }

    public void OnBeginDrag(PointerEventData eventData)//开始拖拽
    {
        if (!isUnlocked || skillData == null)
            return;

        // 检查是否为被动技能，被动技能不允许拖拽
        if (skillData.usageType == SkillUsageType.Passive)
        {
            Debug.LogWarning($"被动技能 {skillData.displayName} 不需要拖拽，解锁即生效！");
            return;
        }

        if (UI_SkillDragHandler.Instance != null)
        {
            UI_SkillDragHandler.Instance.StartDragFromSkillTree(skillData.upgradeType, skillData, this, transform.position);
        }

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)//拖拽中
    {
        if (UI_SkillDragHandler.Instance != null && UI_SkillDragHandler.Instance.IsDragging)
        {
            UI_SkillDragHandler.Instance.UpdateDragPosition(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)//结束拖拽
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (UI_SkillDragHandler.Instance != null)
        {
            UI_SkillDragHandler.Instance.EndDrag(eventData.position);
        }
    }

}
