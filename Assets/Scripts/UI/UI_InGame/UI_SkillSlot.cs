using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UI_SkillSlot : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, ISkillDropTarget
{
    [Header("技能槽位配置")]
    [SerializeField] private int slotIndex; // 槽位索引

    [Header("UI组件")]
    [SerializeField] private Image skillIcon; // 技能图标
    [SerializeField] private Image cooldownOverlay; // 冷却遮罩
    [SerializeField] private TextMeshProUGUI keyText; // 按键文本
    [SerializeField] private TextMeshProUGUI cooldownText; // 冷却时间文本
    [SerializeField] protected Image background; // 背景图片

    [Header("高亮设置")]
    [SerializeField] protected Color highlightColor = Color.yellow; // 高亮颜色

    private Color originalColor; // 原始背景颜色
    private SkillSlotManager skillSlotManager; // 技能槽位管理器引用
    private Player_SkillManager skillManager; // 技能管理器引用
    private SkillUpgradeType currentUpgradeType; // 当前绑定的技能升阶类型
    private Skill_Base currentSkill; // 当前绑定的技能实例
    private CanvasGroup canvasGroup; // 画布组（用于拖拽时的透明度控制）

    public int SlotIndex => slotIndex; // 获取槽位索引
    public SkillUpgradeType CurrentUpgradeType => currentUpgradeType; // 获取当前绑定的技能升阶类型

    protected virtual void Awake() // 初始化组件
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (background != null) originalColor = background.color;
    }

    protected virtual void OnEnable() // 激活时同步管理器当前绑定（读档时序防御：事件可能早于订阅/错过）
    {
        RefreshBindingFromManager();
    }

    protected virtual void Start() // 初始化管理器引用和UI显示
    {
        skillSlotManager = SkillSlotManager.Instance;
        skillManager = Player_SkillManager.Instance;

        RefreshBindingFromManager();
        UpdateKeyText();

        if (skillSlotManager != null)
            skillSlotManager.OnSkillSlotChanged += OnSkillSlotChanged;
        GameInput.OnBindingsChanged += UpdateKeyText;
    }

    // 从管理器同步当前槽位绑定（读档/重激活兜底：不依赖一次性 OnSkillSlotChanged 事件）
    private void RefreshBindingFromManager()
    {
        if (skillSlotManager == null)
            skillSlotManager = SkillSlotManager.Instance;
        if (skillSlotManager == null)
            return;

        SkillUpgradeType boundType = skillSlotManager.GetUpgradeTypeInSlot(slotIndex);
        currentUpgradeType = boundType;
        // skillManager 可能尚未在 Start 中赋值（OnEnable 早于 Start），此时仅同步类型
        currentSkill = (boundType != SkillUpgradeType.None && skillManager != null)
            ? GetSkillByUpgradeType(boundType)
            : null;
        UpdateSlotDisplay();
    }

    private void OnDestroy() // 清理事件监听
    {
        if (skillSlotManager != null)
            skillSlotManager.OnSkillSlotChanged -= OnSkillSlotChanged;
        GameInput.OnBindingsChanged -= UpdateKeyText;
    }

    private void Update() // 每帧更新冷却显示
    {
        UpdateCooldownDisplay();
    }

    private void OnSkillSlotChanged(int index, SkillUpgradeType upgradeType) // 技能槽位改变事件处理
    {
        if (index == slotIndex)
        {
            currentUpgradeType = upgradeType;
            currentSkill = GetSkillByUpgradeType(upgradeType);
            UpdateSlotDisplay();
            UpdateCooldownDisplay(); // 确保冷却UI状态也被更新
        }
    }

    private void UpdateSlotDisplay() // 更新槽位显示
    {
        if (skillIcon == null) return;

        if (currentUpgradeType == SkillUpgradeType.None)
        {
            skillIcon.enabled = false;
        }
        else
        {
            skillIcon.enabled = true;
            Skill_DataSo skillData = GetSkillDataByUpgradeType(currentUpgradeType);
            if (skillData != null && skillData.icon != null)
                skillIcon.sprite = skillData.icon;
        }
    }

    private void UpdateCooldownDisplay() // 更新冷却显示
    {
        if (cooldownOverlay == null) return;

        // 如果没有技能或技能为空，重置冷却UI状态
        if (currentSkill == null || currentUpgradeType == SkillUpgradeType.None)
        {
            cooldownOverlay.fillAmount = 0;
            if (cooldownText != null) cooldownText.gameObject.SetActive(false);
            return;
        }

        float remainingCooldown = currentSkill.GetRemainingCooldown();
        float maxCooldown = currentSkill.Cooldown;

        if (maxCooldown > 0)
        {
            cooldownOverlay.fillAmount = remainingCooldown / maxCooldown;

            if (cooldownText != null)
            {
                if (remainingCooldown > 0)
                {
                    cooldownText.text = remainingCooldown.ToString("F1");
                    cooldownText.gameObject.SetActive(true);
                }
                else
                {
                    cooldownText.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            cooldownOverlay.fillAmount = 0;
            if (cooldownText != null) cooldownText.gameObject.SetActive(false);
        }
    }

    private void UpdateKeyText() // 更新按键文本
    {
        if (keyText == null) return;
        keyText.text = GetSlotKeyDisplay().ToString();
    }

    private KeyCode GetSlotKeyDisplay()
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

    private Skill_DataSo GetSkillDataByUpgradeType(SkillUpgradeType upgradeType) // 从DataManager获取技能数据
    {
        return SkillDataManager.Instance?.GetSkillData(upgradeType);
    }

    private Skill_Base GetSkillByUpgradeType(SkillUpgradeType upgradeType) // 根据技能升阶类型获取技能实例
    {
        if (upgradeType == SkillUpgradeType.None || skillManager == null || skillManager.allSkills == null)
            return null;

        foreach (var skill in skillManager.allSkills)
        {
            if (skill.upgradeType == upgradeType) return skill;
        }
        return null;
    }

    public void OnPointerDown(PointerEventData eventData) // 鼠标按下事件处理
    {
        if (eventData.button == PointerEventData.InputButton.Right && currentUpgradeType != SkillUpgradeType.None)
        {
            skillSlotManager = skillSlotManager ?? SkillSlotManager.Instance;
            skillSlotManager?.UnbindSkillFromSlot(slotIndex);
        }
    }

    public void OnBeginDrag(PointerEventData eventData) // 开始拖拽事件处理
    {
        if (currentUpgradeType == SkillUpgradeType.None) return;

        if (UI_SkillDragHandler.Instance != null)
        {
            Skill_DataSo skillData = GetSkillDataByUpgradeType(currentUpgradeType);
            UI_SkillDragHandler.Instance.StartDragFromSkillSlot(currentUpgradeType, skillData, this, transform.position);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData) // 拖拽中事件处理
    {
        if (UI_SkillDragHandler.Instance != null && UI_SkillDragHandler.Instance.IsDragging)
            UI_SkillDragHandler.Instance.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData) // 结束拖拽事件处理
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        if (UI_SkillDragHandler.Instance != null)
            UI_SkillDragHandler.Instance.EndDrag(eventData.position);
    }

    public virtual void OnPointerEnter(PointerEventData eventData) // 鼠标进入事件处理
    {
        if (UI_SkillDragHandler.Instance == null || !UI_SkillDragHandler.Instance.IsDragging) return;

        SkillUpgradeType draggedUpgradeType = UI_SkillDragHandler.Instance.DraggingUpgradeType;
        if (CanAcceptSkill(draggedUpgradeType) && background != null)
            background.color = highlightColor;
    }

    public virtual void OnPointerExit(PointerEventData eventData) // 鼠标离开事件处理
    {
        if (background != null) background.color = originalColor;
    }

    public virtual bool CanAcceptSkill(SkillUpgradeType upgradeType) // 检查是否可以接受该技能
    {
        return upgradeType != SkillUpgradeType.None;
    }

    public virtual void OnSkillDropped(SkillUpgradeType upgradeType, Skill_DataSo skillData, UI_TreeNode sourceNode, UI_SkillSlot sourceSlot) // 技能放置事件处理
    {
        skillSlotManager = skillSlotManager ?? SkillSlotManager.Instance;

        if (skillSlotManager == null) 
            return;

        if (sourceSlot != null && sourceSlot == this)
            return;

        if (sourceSlot != null)
        {
            skillSlotManager.SwapSkills(sourceSlot.SlotIndex, slotIndex);
        }
        else
        {
            SkillType skillType = GetSkillTypeByUpgradeType(upgradeType);
            if (skillType != SkillType.None)
                AutoUnbindSameTypeSkills(skillType, slotIndex);

            SkillUpgradeType currentUpgradeType = skillSlotManager.GetUpgradeTypeInSlot(slotIndex);
            if (currentUpgradeType != SkillUpgradeType.None)
                skillSlotManager.UnbindSkillFromSlot(slotIndex);

            skillSlotManager.BindSkillToSlot(upgradeType, slotIndex);
        }
    }

    private void AutoUnbindSameTypeSkills(SkillType skillType, int currentSlotIndex) // 自动解绑相同大类的技能
    {
        if (skillSlotManager == null) return;

        for (int i = 0; i < skillSlotManager.GetSlotCount(); i++)
        {
            if (i == currentSlotIndex) continue;

            SkillUpgradeType existingUpgradeType = skillSlotManager.GetUpgradeTypeInSlot(i);
            if (existingUpgradeType != SkillUpgradeType.None)
            {
                SkillType existingSkillType = GetSkillTypeByUpgradeType(existingUpgradeType);
                if (existingSkillType == skillType)
                    skillSlotManager.UnbindSkillFromSlot(i);
            }
        }
    }

    private SkillType GetSkillTypeByUpgradeType(SkillUpgradeType upgradeType) // 从DataManager获取技能大类型
    {
        if (upgradeType == SkillUpgradeType.None) return SkillType.None;
        return SkillDataManager.Instance?.GetSkillType(upgradeType) ?? SkillType.None;
    }
}