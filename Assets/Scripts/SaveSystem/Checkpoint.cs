using DG.Tweening;
using UnityEngine;

/// <summary>
/// 检查点。玩家进入触发区按 F 存档。
/// 提示 UI 进入时淡入 + 上下浮动，离开时淡出。
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Header("检查点配置")]
    [SerializeField] private string checkpointId;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private bool isEntryPoint; // 是否作为本区域的入口存档点（传送门到达位置）

    public bool IsEntryPoint => isEntryPoint; // 是否入口存档点（供传送门/玩家生成器查询）
    public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position; // 重生/出生位置

    [Header("提示 UI")]
    [SerializeField] private GameObject promptRoot;         // "按 F 存档" UI 根对象

    [Header("光效（可选）")]
    [SerializeField] private GameObject saveEffectPrefab;  // 存档成功特效

    [Header("参数")]
    [SerializeField] private float cooldown = 2f;           // 存档冷却
    [SerializeField] private float fadeDuration = 0.25f;    // 淡入淡出时长
    [SerializeField] private float floatHeight = 0.2f;      // 浮动幅度
    [SerializeField] private float floatSpeed = 2f;         // 浮动速度

    private bool playerInRange;
    private float lastSaveTime = -10f;
    private CanvasGroup cg;
    private Vector3 promptBasePos;
    private Tween floatTween;

    private void Awake()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);

            // 自动添加 CanvasGroup（淡入淡出用）
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
        if (Time.time - lastSaveTime < cooldown) return;

        lastSaveTime = Time.time;
        DoSave();
    }

    private void DoSave()
    {
        if (SaveManager.Instance == null) return;

        SaveManager.Instance.CurrentCheckpointId = checkpointId;
        SaveManager.Instance.Save();

        // 光效（重生点位播放）
        if (saveEffectPrefab != null)
        {
            Vector3 pos = respawnPoint != null ? respawnPoint.position : transform.position;
            GameObject effect = Instantiate(saveEffectPrefab, pos, Quaternion.identity);
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
            Destroy(effect, 3f);  // 播完自动清理
        }

        // 音效
        AudioManager.Instance?.PlaySaveSfx();

        // 事件提示
        var eventTip = FindAnyObjectByType<UI_EventTip>();
        eventTip?.ShowSaveSuccess();

        // 提示闪一下反馈
        if (promptRoot != null)
            StartCoroutine(FlashPrompt());
    }

    private System.Collections.IEnumerator FlashPrompt()
    {
        cg.alpha = 0;
        yield return new WaitForSeconds(0.12f);
        if (playerInRange) cg.DOFade(1, fadeDuration);
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

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = respawnPoint != null ? respawnPoint.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, 0.3f);
    }
}
