using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropTargetHighlighter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("高亮设置")]
    [SerializeField] private Image highlightImage;
    [SerializeField] private Color acceptColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
    [SerializeField] private Color rejectColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private bool autoFindHighlightImage = true;

    private Color originalColor;
    private bool hasOriginalColor;

    private void Awake()
    {
        InitializeHighlightImage();
    }

    private void InitializeHighlightImage()
    {
        if (highlightImage != null)
        {
            hasOriginalColor = true;
            originalColor = highlightImage.color;
            return;
        }

        if (autoFindHighlightImage)
        {
            highlightImage = GetComponent<Image>();

            if (highlightImage != null)
            {
                hasOriginalColor = true;
                originalColor = highlightImage.color;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UI_ItemDragHandler.Instance == null || !UI_ItemDragHandler.Instance.IsDragging)
            return;

        if (highlightImage == null)
            return;

        Inventory_Item draggedItem = UI_ItemDragHandler.Instance.DraggingItem;

        if (draggedItem == null)
            return;

        IItemDropTarget dropTarget = GetComponent<IItemDropTarget>();

        if (dropTarget == null)
            return;

        bool canAccept = dropTarget.CanAcceptItem(draggedItem);

        if (canAccept)
        {
            SetHighlightColor(acceptColor);
        }
        else
        {
            SetHighlightColor(rejectColor);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHighlight();
    }

    private void SetHighlightColor(Color color)
    {
        if (highlightImage == null)
            return;

        highlightImage.color = color;
    }

    private void ClearHighlight()
    {
        if (highlightImage == null)
            return;

        if (hasOriginalColor)
        {
            highlightImage.color = originalColor;
        }
        else
        {
            Color clearColor = highlightImage.color;
            clearColor.a = 0f;
            highlightImage.color = clearColor;
        }
    }

    public void SetHighlightImage(Image image)
    {
        highlightImage = image;

        if (highlightImage != null)
        {
            hasOriginalColor = true;
            originalColor = highlightImage.color;
        }
    }

    public void SetAcceptColor(Color color)
    {
        acceptColor = color;
    }

    public void SetRejectColor(Color color)
    {
        rejectColor = color;
    }
}
