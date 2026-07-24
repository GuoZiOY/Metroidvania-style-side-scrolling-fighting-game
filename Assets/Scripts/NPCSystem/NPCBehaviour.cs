using DG.Tweening;
using TMPro;
using UnityEngine;

// NPC 交互组件。进入触发区显示提示，按 F 打开商店。
// 提示 UI 效果与存档点一致：淡入淡出 + 上下浮动。
public class NPCBehaviour : MonoBehaviour
{
    [Header("NPC 数据")]
    [SerializeField] private string npcId;
    [SerializeField] private string npcName;

    [Header("商店（可选）")]
    [SerializeField] private ShopSO shopData;

    [Header("交互提示")]
    [SerializeField] private GameObject promptRoot;        // "按 F 交互" UI

    [Header("提示参数")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float floatHeight = 0.2f;
    [SerializeField] private float floatSpeed = 2f;

    private bool playerInRange;
    private CanvasGroup cg;
    private Vector3 promptBasePos;
    private Tween floatTween;

    private void Awake()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);

            cg = promptRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = promptRoot.AddComponent<CanvasGroup>();
            cg.alpha = 0;

            promptBasePos = promptRoot.transform.localPosition;
        }
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

        // 商店已打开 → 关闭
        if (UI_ShopPanel.IsShopOpen)
        {
            UI_ShopPanel.Instance.Close();
            return;
        }

        // 未打开 → 打开
        if (shopData != null)
        {
            var shopUI = UI_ShopPanel.Instance;
            if (shopUI != null)
                shopUI.Open(shopData, npcName);
        }
    }

    private void ShowPrompt(bool show)
    {
        if (promptRoot == null || cg == null) return;

        cg.DOKill();

        if (show)
        {
            promptRoot.SetActive(true);
            cg.alpha = 0;
            cg.DOFade(1, fadeDuration);
            StartFloating();
        }
        else
        {
            cg.DOFade(0, fadeDuration).OnComplete(() => promptRoot.SetActive(false));
            StopFloating();
        }
    }

    private void StartFloating()
    {
        StopFloating();
        if (promptRoot == null) return;

        floatTween = DOTween.To(
            () => 0f,
            t => promptRoot.transform.localPosition = promptBasePos + Vector3.up * Mathf.Sin(t * floatSpeed) * floatHeight,
            Mathf.PI * 2f,
            Mathf.PI * 2f / floatSpeed
        ).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear);
    }

    private void StopFloating()
    {
        floatTween?.Kill();
        floatTween = null;
        if (promptRoot != null)
            promptRoot.transform.localPosition = promptBasePos;
    }

    private void OnDestroy()
    {
        floatTween?.Kill();
    }
}
