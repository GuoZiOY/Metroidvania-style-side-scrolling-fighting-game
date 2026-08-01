using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

// NPC 交互组件。进入触发区显示提示，按 F 打开商店。
// 提示 UI 效果与存档点一致：淡入淡出 + 上下浮动。
public class NPCBehaviour : MonoBehaviour
{
    [Header("NPC 数据")]
    [SerializeField] private string npcId;
    public string npcName;

    [Header("关联任务")]
    public List<QuestData> questsToGive;

    [Header("商店（可选）")]
    public ShopSO shopData;

    [Header("工作台（铁匠）")]
    public bool hasWorkbench;   // 是否为铁匠 NPC——对话菜单显示"打开工作台"（打开合成/分解/制作面板）

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

        // 已打开 → 关闭（统一走 UIManager，不依赖商店面板单例）
        if (UIManager.Instance != null && UIManager.Instance.IsShopOpen)
        {
            UIManager.Instance.CloseShop();
            return;
        }

        // 通知任务系统：玩家与此 NPC 对话（推进 TalkToNPC 目标）
        if (!string.IsNullOrEmpty(npcId))
            QuestEvents.ReportNpcTalked(npcId);

        // 打开公用的对话菜单（场景中一份，所有 NPC 共享）——统一走 UIManager
        if (HasAnyInteraction())
        {
            UIManager.Instance?.ShowNpcMenu(this);
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

    // ═════════════════════════════════════════════
    //  任务交互（原 NPCQuestGiver）
    // ═════════════════════════════════════════════

    private QuestManager qm => QuestManager.Instance;

    // 有任务/商店/工作台任一交互即显示对话菜单（铁匠仅工作台也需能打开）
    public bool HasAnyInteraction() => (questsToGive?.Count > 0) || shopData != null || hasWorkbench;

    // ─── 简化查询：用谓词过滤任务列表 ───

    private QuestData FirstQuestWhere(System.Func<QuestData, bool> predicate)
    {
        if (questsToGive == null) return null;
        foreach (var q in questsToGive)
            if (q != null && predicate(q)) return q;
        return null;
    }

    private bool AnyQuestWhere(System.Func<QuestData, bool> predicate)
    {
        return FirstQuestWhere(predicate) != null;
    }

    public bool HasAvailableQuest()     => AnyQuestWhere(q => qm != null && qm.IsQuestAvailable(q.questId));
    public bool HasActiveQuest()        => AnyQuestWhere(q => qm != null && qm.IsActive(q.questId));
    public bool HasFinalRewardToClaim() => AnyQuestWhere(q => qm != null && qm.IsReadyToClaim(q.questId));

    public QuestData GetFirstAvailableQuest()    => FirstQuestWhere(q => qm != null && qm.IsQuestAvailable(q.questId));
    public QuestData GetFirstActiveQuest()       => FirstQuestWhere(q => qm != null && qm.IsActive(q.questId));
    public QuestData GetFirstReadyToClaimQuest() => FirstQuestWhere(q => qm != null && qm.IsReadyToClaim(q.questId));

    private bool IsStageCompleteAndRewardable(QuestData q)
    {
        if (qm == null || !qm.IsCurrentStageComplete(q.questId)) return false;
        var stage = qm.GetCurrentStage(q.questId);
        if (stage == null || q.stages == null) return false;
        int idx = q.stages.IndexOf(stage);

        // 非最终阶段 → 必须提交
        if (idx < q.stages.Count - 1) return true;

        // 最终阶段 → 有阶段奖励或收集目标都需要先提交物品
        return (stage.stageReward != null && stage.stageReward.HasReward) || stage.HasCollectObjective;
    }

    public bool HasStageToSubmit()        => AnyQuestWhere(IsStageCompleteAndRewardable);
    public QuestData GetFirstStageToSubmit() => FirstQuestWhere(IsStageCompleteAndRewardable);
}
