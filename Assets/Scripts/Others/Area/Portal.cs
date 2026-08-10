using UnityEngine;
using UnityEngine.SceneManagement;

// 传送门：进入触发区按 F 传送到目标场景。
// 到达后由 PlayerSpawner 把持久玩家定位到目标场景的入口存档点（isEntryPoint）。
public class Portal : MonoBehaviour
{
    [Header("传送配置")]
    [SerializeField] private string targetScene;             // 目标场景名（需加入 Build Settings）
    [SerializeField] private bool saveBeforeTeleport = true; // 传送前是否存档

    [Header("提示 UI")]
    [SerializeField] private GameObject promptRoot; // "按 F 传送" 提示

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
        if (promptRoot != null)
            promptRoot.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (!GameInput.GetKeyDown(GameInput.Action.Interact)) return;
        Teleport();
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

        // 标记到达后在目标场景入口存档点生成，并重新存档
        PlayerSpawner.MarkSpawnAtEntry(true);

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
