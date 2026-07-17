using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_AttributeManager : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private TextMeshProUGUI availablePointsText;
    [SerializeField] private TextMeshProUGUI usedPointsText;
    [SerializeField] private Button resetButton;

    private void Start()
    {
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetAllAttributes);
        }

        UpdateDisplay();
    }

    private void Update()
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (PlayerLevelManager.Instance == null)
            return;

        int availablePoints = PlayerLevelManager.Instance.AttributePoints;

        if (availablePointsText != null)
        {
            availablePointsText.text = $"可用属性点: {availablePoints}";
        }

        if (AttributePointManager.Instance != null && usedPointsText != null)
        {
            int usedPoints = AttributePointManager.Instance.TotalAttributePoints;
            usedPointsText.text = $"已用属性点: {usedPoints}";
        }

        if (resetButton != null && AttributePointManager.Instance != null)
        {
            resetButton.interactable = AttributePointManager.Instance.TotalAttributePoints > 0;
        }
    }

    private void ResetAllAttributes()
    {
        if (AttributePointManager.Instance != null)
        {
            AttributePointManager.Instance.ResetAttributePoints();
        }
    }
}
