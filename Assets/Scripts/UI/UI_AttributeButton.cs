using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class UI_AttributeButton : MonoBehaviour, IPointerClickHandler
{
    [Header("属性配置")]
    [SerializeField] private StatType attributeType;

    [Header("UI组件")]
    [SerializeField] private Button addButton;

    private Player player;

    private void Start()
    {
        player = FindFirstObjectByType<Player>();

        if (addButton != null)
        {
            addButton.onClick.AddListener(AddPoint);
        }

        UpdateDisplay();
    }

    private void Update()
    {
        UpdateDisplay();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            AddPoint();
        }
    }

    private void AddPoint()
    {
        if (AttributePointManager.Instance != null)
        {
            AttributePointManager.Instance.AllocateAttributePoint(attributeType);
        }
    }

    private void UpdateDisplay()
    {
        if (player == null || player.stats == null)
            return;

        if (addButton != null && PlayerLevelManager.Instance != null)
        {
            addButton.interactable = PlayerLevelManager.Instance.AttributePoints > 0;
        }
    }
}
