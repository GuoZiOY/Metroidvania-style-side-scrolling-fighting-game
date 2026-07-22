using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 存档槽位组件。挂载在槽位预制体根对象上。
/// 自动收集子节点的 UI 组件引用。
/// </summary>
public class SaveSlot : MonoBehaviour
{
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI sceneText;
    public TextMeshProUGUI playTimeText;
    public TextMeshProUGUI dateText;
    public GameObject emptyGroup;
    public GameObject infoGroup;
    public GameObject highlight;
    public Button clickBtn;

    private void Reset()
    {
        // 在编辑器里点 Reset 时自动查找子组件
        AutoFindComponents();
    }

    /// <summary>自动查找子节点中的 UI 组件</summary>
    public void AutoFindComponents()
    {
        var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmps)
        {
            switch (tmp.name)
            {
                case "LevelText":   levelText = tmp; break;
                case "SceneText":   sceneText = tmp; break;
                case "PlayTimeText": playTimeText = tmp; break;
                case "DateText":    dateText = tmp; break;
            }
        }

        var btns = GetComponentsInChildren<Button>(true);
        foreach (var btn in btns)
        {
            if (btn.name == "ClickBtn" || btn.GetComponentInParent<SaveSlot>() == this)
                clickBtn = btn;
        }

        // 按名称查找子节点
        foreach (Transform child in transform)
        {
            switch (child.name)
            {
                case "EmptyGroup":   emptyGroup = child.gameObject; break;
                case "InfoGroup":    infoGroup = child.gameObject; break;
                case "Highlight":    highlight = child.gameObject; break;
            }
        }
    }
}
