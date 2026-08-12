using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

// 传送门：进入触发区按 F 传送到目标场景。
// 到达后由 PlayerSpawner 把持久玩家定位到目标场景中 portalId 对应的传送门落点
// （未配置 targetPortalId 或目标传送门找不到时，回退到入口存档点 isEntryPoint）。
public class Portal : MonoBehaviour
{
    [Header("传送配置")]
    [SerializeField] private string portalId;                // 本传送门唯一 ID（供目标场景的传送定位）
    [SerializeField] private string targetScene;             // 目标场景名（需加入 Build Settings）
    [SerializeField] private string targetPortalId;          // 目标场景中对应的传送门 ID（到达时定位到它；留空=回入口存档点）
    [SerializeField] private Transform spawnPoint;           // 到达时玩家的落点（未指定用本传送门位置）
    [SerializeField] private bool saveBeforeTeleport = true; // 传送前是否存档

    public string PortalId => portalId; // 本传送门 ID（PlayerSpawner 按它定位）
    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position; // 传送落点

    [Header("提示 UI")]
    [SerializeField] private GameObject promptRoot; // "按 F 传送" 提示

    [Header("提示参数（与 NPC 一致：淡入淡出 + 上下浮动）")]
    [SerializeField] private float fadeDuration = 0.25f; // 淡入淡出时长
    [SerializeField] private float floatHeight = 0.2f;   // 浮动高度
    [SerializeField] private float floatSpeed = 2f;      // 浮动速度

    private bool playerInRange;
    private bool isLocked; // 锁定后禁止交互（Boss 战期间到达传送门锁定用）
    private CanvasGroup cg;
    private Vector3 promptBasePos;
    private Tween floatTween;

    private void Awake()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);

            cg = promptRoot.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = promptRoot.AddComponent<CanvasGroup>();
            cg.alpha = 0;

            promptBasePos = promptRoot.transform.localPosition;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isLocked) return; // 锁定期间不显示提示
        if (!other.CompareTag("Player"))
            return;
        playerInRange = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;
        playerInRange = false;
        ShowPrompt(false);
    }

    // 锁定/解锁（BossEncounter 调用；锁定后禁交互并隐藏提示）
    public void SetLocked(bool value)
    {
        isLocked = value;
        if (value)
            ShowPrompt(false);
    }

    private void Update()
    {
        if (isLocked) return; // 锁定期间不可交互
        if (!playerInRange) return;
        if (!GameInput.GetKeyDown(GameInput.Action.Interact)) return;
        Teleport();
    }

    // 提示显示/隐藏：淡入淡出 + 上下浮动（与 NPC 交互提示效果一致）
    private void ShowPrompt(bool show)
    {
        if (promptRoot == null || cg == null)
            return;

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
        if (promptRoot == null)
            return;

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

    private void Teleport()
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("[Portal] 未设置目标场景 targetScene");
            return;
        }

        // 传送前存档：保留当前进度（位置会在到达后覆盖为入口存档点）
        if (saveBeforeTeleport && SaveManager.Instance != null && SaveManager.Instance.CurrentSlotIndex >= 0)
            SaveManager.Instance.Save();

        // 标记到达目标场景的对应传送门（找不到时回退入口存档点），并重新存档
        PlayerSpawner.MarkSpawnAtPortal(targetPortalId, true);

        // 过场黑幕过渡：淡出 → 切场景 → 新场景就绪后淡入
        SceneTransitionFader.Instance.TransitionToScene(targetScene);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        if (!string.IsNullOrEmpty(targetScene))
        {
            Vector3 labelPos = transform.position + Vector3.up * 1.5f;
            Gizmos.DrawLine(transform.position, labelPos);
        }
    }
}
