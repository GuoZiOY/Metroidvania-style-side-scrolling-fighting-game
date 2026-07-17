using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_KeyRebind : MonoBehaviour
{
    [SerializeField] private GameInput.Action action;
    [SerializeField] private TextMeshProUGUI keyLabel;
    [SerializeField] private Button rebindButton;

    private bool isRebinding;

    private void Start()
    {
        UpdateLabel();
        if (rebindButton != null)
            rebindButton.onClick.AddListener(StartRebind);
    }

    private void UpdateLabel()
    {
        if (keyLabel == null) return;
        keyLabel.text = GameInput.GetBinding(action).ToString();
    }

    private void StartRebind()
    {
        if (isRebinding) return;
        isRebinding = true;
        if (keyLabel != null)
            keyLabel.text = "按下按键...";
    }

    private void OnGUI()
    {
        if (!isRebinding) return;
        if (!Event.current.isKey || Event.current.keyCode == KeyCode.None)
            return;

        GameInput.SetBinding(action, Event.current.keyCode);
        isRebinding = false;
        UpdateLabel();
    }

    public void ResetToDefault()
    {
        GameInput.ResetToDefaults();
        UpdateLabel();
    }
}
