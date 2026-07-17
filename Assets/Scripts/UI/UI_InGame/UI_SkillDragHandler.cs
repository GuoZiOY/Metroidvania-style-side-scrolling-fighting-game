using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UI_SkillDragHandler : MonoBehaviour
{
    public static UI_SkillDragHandler Instance { get; private set; }

    [SerializeField] private GameObject dragVisualPrefab; // 拖拽视觉预制体
    [SerializeField] private Canvas uiCanvas; // UI画布引用

    private GameObject currentDragVisual; // 当前拖拽视觉对象
    private Image dragVisualImage; // 拖拽视觉图像组件
    private SkillUpgradeType draggingUpgradeType; // 正在拖拽的技能升阶类型
    private Skill_DataSo draggingSkillData; // 正在拖拽的技能数据
    private UI_TreeNode sourceNode; // 源技能树节点
    private UI_SkillSlot sourceSlot; // 源技能槽位

    private Queue<GameObject> dragVisualPool = new Queue<GameObject>(); // 拖拽视觉对象池

    public bool IsDragging { get; private set; } // 是否正在拖拽
    public SkillUpgradeType DraggingUpgradeType => draggingUpgradeType; // 获取正在拖拽的技能升阶类型
    public Skill_DataSo DraggingSkillData => draggingSkillData; // 获取正在拖拽的技能数据
    public bool IsDraggingFromSkillTree => sourceNode != null; // 是否从技能树拖拽
    public bool IsDraggingFromSkillSlot => sourceSlot != null; // 是否从技能槽位拖拽

    private void Awake() // 初始化单例
    {
        if (Instance != null && Instance != this) // 检查是否已存在实例
        {
            Destroy(gameObject); // 销毁重复实例
            return;
        }
        Instance = this; // 设置当前实例为单例
    }

    public void StartDragFromSkillTree(SkillUpgradeType upgradeType, Skill_DataSo skillData, UI_TreeNode sourceNode, Vector2 startPos) // 从技能树开始拖拽
    {
        if (upgradeType == SkillUpgradeType.None || skillData == null || sourceNode == null) // 检查参数是否有效
            return;

        draggingUpgradeType = upgradeType; // 设置拖拽技能升阶类型
        draggingSkillData = skillData; // 设置拖拽技能数据
        this.sourceNode = sourceNode; // 设置源节点
        this.sourceSlot = null; // 清空源槽位
        IsDragging = true; // 设置拖拽状态

        CreateDragVisual(skillData, startPos); // 创建拖拽视觉对象
    }

    public void StartDragFromSkillSlot(SkillUpgradeType upgradeType, Skill_DataSo skillData, UI_SkillSlot sourceSlot, Vector2 startPos) // 从技能槽位开始拖拽
    {
        if (upgradeType == SkillUpgradeType.None || skillData == null || sourceSlot == null) // 检查参数是否有效
            return;

        draggingUpgradeType = upgradeType; // 设置拖拽技能升阶类型
        draggingSkillData = skillData; // 设置拖拽技能数据
        this.sourceNode = null; // 清空源节点
        this.sourceSlot = sourceSlot; // 设置源槽位
        IsDragging = true; // 设置拖拽状态

        CreateDragVisual(skillData, startPos); // 创建拖拽视觉对象
    }

    public void UpdateDragPosition(Vector2 position) // 更新拖拽位置
    {
        if (!IsDragging || currentDragVisual == null) // 检查是否正在拖拽
            return;

        currentDragVisual.transform.position = position; // 更新视觉对象位置
    }

    public void EndDrag(Vector2 mousePosition) // 结束拖拽
    {
        if (!IsDragging) // 检查是否正在拖拽
            return;

        PerformDropDetection(mousePosition); // 执行放置检测
        CleanupDrag(); // 清理拖拽状态
    }

    public void CleanupDrag() // 清理拖拽状态
    {
        if (currentDragVisual != null) // 如果拖拽视觉对象存在
        {
            currentDragVisual.SetActive(false); // 隐藏视觉对象
            dragVisualPool.Enqueue(currentDragVisual); // 将对象回收到对象池
            currentDragVisual = null; // 清空当前视觉对象引用
        }

        draggingUpgradeType = SkillUpgradeType.None; // 重置拖拽技能升阶类型
        draggingSkillData = null; // 重置拖拽技能数据
        sourceNode = null; // 清空源节点
        sourceSlot = null; // 清空源槽位
        IsDragging = false; // 重置拖拽状态
    }

    private void CreateDragVisual(Skill_DataSo skillData, Vector2 position) // 创建拖拽视觉对象
    {
        if (dragVisualPrefab == null || uiCanvas == null) // 检查预制体和画布是否存在
            return;

        if (dragVisualPool.Count > 0) // 如果对象池中有可用对象
        {
            currentDragVisual = dragVisualPool.Dequeue(); // 从对象池获取对象
            currentDragVisual.SetActive(true); // 激活对象
        }
        else // 如果对象池为空
        {
            currentDragVisual = Instantiate(dragVisualPrefab, uiCanvas.transform); // 实例化新对象
        }

        currentDragVisual.transform.position = position; // 设置对象位置

        dragVisualImage = currentDragVisual.GetComponent<Image>(); // 获取图像组件
        if (dragVisualImage != null && skillData != null && skillData.icon != null) // 如果组件和图标存在
        {
            dragVisualImage.sprite = skillData.icon; // 设置图标
        }

        CanvasGroup canvasGroup = currentDragVisual.GetComponent<CanvasGroup>(); // 获取画布组组件
        if (canvasGroup == null) // 如果画布组不存在
        {
            canvasGroup = currentDragVisual.AddComponent<CanvasGroup>(); // 添加画布组组件
        }
        canvasGroup.blocksRaycasts = false; // 禁用射线检测
        canvasGroup.alpha = 0.8f; // 设置透明度
    }

    private void PerformDropDetection(Vector2 mousePosition) // 执行放置检测
    {
        if (draggingUpgradeType == SkillUpgradeType.None) // 检查是否正在拖拽技能
        {
            return;
        }

        ISkillDropTarget dropTarget = FindValidDropTarget(mousePosition); // 查找有效的放置目标

        if (dropTarget != null) // 如果找到放置目标
        {
            // 直接调用放置操作，让目标自己处理冲突逻辑
            dropTarget.OnSkillDropped(draggingUpgradeType, draggingSkillData, sourceNode, sourceSlot); // 执行放置操作
        }
        else // 如果没有找到放置目标
        {
            ReturnToSource(); // 返回源位置
        }
    }

    private ISkillDropTarget FindValidDropTarget(Vector2 mousePosition) // 查找有效的放置目标
    {
        PointerEventData pointerEventData = new PointerEventData(UnityEngine.EventSystems.EventSystem.current) // 创建指针事件数据
        {
            position = mousePosition // 设置鼠标位置
        };

        List<RaycastResult> raycastResults = new List<RaycastResult>(); // 创建射线检测结果列表
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerEventData, raycastResults); // 执行射线检测

        for (int i = 0; i < raycastResults.Count; i++) // 遍历所有射线检测结果
        {
            var raycastResult = raycastResults[i]; // 获取射线结果
            GameObject hitObject = raycastResult.gameObject; // 获取命中的游戏对象

            ISkillDropTarget dropTarget = hitObject.GetComponent<ISkillDropTarget>(); // 获取放置目标组件

            if (dropTarget == null) // 如果组件不存在
            {
                dropTarget = hitObject.GetComponentInParent<ISkillDropTarget>(); // 在父对象中查找
            }

            if (dropTarget != null) // 如果找到放置目标
            {
                GameObject targetObject = dropTarget as MonoBehaviour != null ? (dropTarget as MonoBehaviour).gameObject : hitObject; // 获取目标对象

                if (targetObject == sourceSlot?.gameObject) // 如果目标是源槽位
                {
                    continue; // 跳过
                }

                return dropTarget; // 返回放置目标
            }
        }

        return null; // 返回空
    }

    private void ReturnToSource() // 返回源位置
    {
        if (sourceSlot != null) // 如果源槽位存在
        {
            CanvasGroup sourceCanvasGroup = sourceSlot.GetComponent<CanvasGroup>(); // 获取源槽位的画布组
            if (sourceCanvasGroup != null) // 如果画布组存在
            {
                sourceCanvasGroup.alpha = 1f; // 恢复不透明
                sourceCanvasGroup.blocksRaycasts = true; // 启用射线检测
            }
        }
    }
}
