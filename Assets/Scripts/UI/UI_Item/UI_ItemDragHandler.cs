using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UI_ItemDragHandler : MonoBehaviour
{
    public static UI_ItemDragHandler Instance { get; private set; }

    [SerializeField] private GameObject dragVisualPrefab;
    [SerializeField] private Canvas uiCanvas;

    private GameObject currentDragVisual;
    private Image dragVisualImage;
    private Inventory_Item draggingItem;
    private UI_ItemSlot sourceSlot;

    private Queue<GameObject> dragVisualPool = new Queue<GameObject>();//拖拽视觉对象池

    public bool IsDragging { get; private set; }
    public Inventory_Item DraggingItem => draggingItem;
    public bool IsDraggingFromEquipment => sourceSlot is UI_EquipSlot;
    public bool IsDraggingFromWarehouse => sourceSlot is UI_WarehouseSlot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void StartDrag(Inventory_Item item, UI_ItemSlot sourceSlot, Vector2 startPos)
    {
        if (item == null || sourceSlot == null)
            return;

        draggingItem = item;
        this.sourceSlot = sourceSlot;
        IsDragging = true;

        CreateDragVisual(item, startPos);
    }

    public void UpdateDragPosition(Vector2 position)
    {
        if (!IsDragging || currentDragVisual == null)
            return;

        currentDragVisual.transform.position = position;
    }

    public void EndDrag(Vector2 mousePosition)
    {
        if (!IsDragging)
            return;

        PerformDropDetection(mousePosition);
        CleanupDrag();
    }

    public void CleanupDrag()
    {
        if (currentDragVisual != null)
        {
            currentDragVisual.SetActive(false);
            dragVisualPool.Enqueue(currentDragVisual);
            currentDragVisual = null;
        }

        draggingItem = null;
        sourceSlot = null;
        IsDragging = false;
    }

    private void CreateDragVisual(Inventory_Item item, Vector2 position)
    {
        if (dragVisualPrefab == null || uiCanvas == null)
        {
            Debug.LogError("拖拽视觉预制体或UI Canvas未设置");
            return;
        }

        if (dragVisualPool.Count > 0)
        {
            currentDragVisual = dragVisualPool.Dequeue();
            currentDragVisual.SetActive(true);
        }
        else
        {
            currentDragVisual = Instantiate(dragVisualPrefab, uiCanvas.transform);
        }

        currentDragVisual.transform.position = position;

        dragVisualImage = currentDragVisual.GetComponent<Image>();
        if (dragVisualImage != null && item.itemData != null && item.itemData.itemIcon != null)
        {
            dragVisualImage.sprite = item.itemData.itemIcon;
        }

        CanvasGroup canvasGroup = currentDragVisual.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = currentDragVisual.AddComponent<CanvasGroup>();
        }
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.8f;
    }

    private void PerformDropDetection(Vector2 mousePosition)
    {
        if (draggingItem == null)
        {
            Debug.LogWarning("拖拽物品为空，无法进行投放检测");
            return;
        }

        IItemDropTarget dropTarget = FindValidDropTarget(mousePosition);

        if (dropTarget != null)
        {
            if (dropTarget.CanAcceptItem(draggingItem))
            {
                dropTarget.OnItemDropped(draggingItem, sourceSlot);
            }
            else
            {
                ReturnItemToSourceSlot();
            }
        }
        else
        {
            ReturnItemToSourceSlot();
        }
    }

    private IItemDropTarget FindValidDropTarget(Vector2 mousePosition)
    {
        PointerEventData pointerEventData = new PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = mousePosition
        };

        System.Collections.Generic.List<RaycastResult> raycastResults = new System.Collections.Generic.List<RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            var raycastResult = raycastResults[i];
            GameObject hitObject = raycastResult.gameObject;

            IItemDropTarget dropTarget = hitObject.GetComponent<IItemDropTarget>();

            if (dropTarget == null)
            {
                dropTarget = hitObject.GetComponentInParent<IItemDropTarget>();
            }

            if (dropTarget != null)
            {
                GameObject targetObject = dropTarget as MonoBehaviour != null ? (dropTarget as MonoBehaviour).gameObject : hitObject;

                if (targetObject == sourceSlot.gameObject)
                {
                    continue;
                }

                return dropTarget;
            }
        }

        return null;
    }

    private void ReturnItemToSourceSlot()
    {
        if (sourceSlot == null)
        {
            Debug.LogError("源槽位为空，无法返回物品");
            return;
        }

        CanvasGroup sourceCanvasGroup = sourceSlot.GetComponent<CanvasGroup>();
        if (sourceCanvasGroup != null)
        {
            sourceCanvasGroup.alpha = 1f;
            sourceCanvasGroup.blocksRaycasts = true;
        }
    }
}
