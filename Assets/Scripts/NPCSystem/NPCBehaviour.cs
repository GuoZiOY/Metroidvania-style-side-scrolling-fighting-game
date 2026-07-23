using TMPro;
using UnityEngine;

/// <summary>
/// NPC 交互组件。进入触发区显示提示，按 F 打开商店或对话。
/// </summary>
public class NPCBehaviour : MonoBehaviour
{
    [Header("NPC 数据")]
    [SerializeField] private string npcId;
    [SerializeField] private string npcName;

    [Header("商店（可选）")]
    [SerializeField] private ShopSO shopData;

    [Header("交互提示")]
    [SerializeField] private GameObject promptRoot;        // "按 F 交互" UI
    [SerializeField] private TextMeshProUGUI promptText;

    private bool playerInRange;

    private void Awake()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        ShowPrompt(false);
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (!GameInput.GetKeyDown(GameInput.Action.Interact)) return;

        OnInteract();
    }

    private void OnInteract()
    {
        // 商店面板待实现
        //if (shopData != null)
        //{
        //    var shopUI = FindAnyObjectByType<UI_ShopPanel>();
        //    if (shopUI != null)
        //        shopUI.Open(shopData);
        //}
    }

    private void ShowPrompt(bool show)
    {
        if (promptRoot == null) return;
        promptRoot.SetActive(show);
    }
}
