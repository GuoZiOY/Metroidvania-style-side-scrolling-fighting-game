using UnityEngine;
using UnityEngine.UI;

public class PanelSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject[] panels;
    [SerializeField] private Button[] buttons;
    [SerializeField] private Color selectedColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color normalColor = Color.white;

    private void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => ShowPanel(index));
        }
        if (panels.Length > 0)
            ShowPanel(0);
    }

    public void ShowPanel(int index)
    {
        if (index < 0 || index >= panels.Length) return;

        for (int i = 0; i < panels.Length; i++)
            panels[i].SetActive(i == index);

        for (int i = 0; i < buttons.Length; i++)
            SetButtonColor(buttons[i], i == index ? selectedColor : normalColor);
    }

    private static void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color;
        cb.pressedColor = color;
        cb.selectedColor = color;
        btn.colors = cb;
    }
}
