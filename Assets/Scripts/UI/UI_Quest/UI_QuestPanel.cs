using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 任务日志面板——只读。
// 按类型分页签显示任务列表，点击查看详情和进度。
// 不包含接受/提交/领奖操作，全部统一走 NPC 对话。
public class UI_QuestPanel : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panelRoot;

    [Header("任务列表")]
    [SerializeField] private RectTransform questListContent;
    [SerializeField] private GameObject questEntryPrefab;

    [Header("分类页签")]
    [SerializeField] private Button tabMain;
    [SerializeField] private Button tabSide;
    [SerializeField] private Button tabTemporary;
    [SerializeField] private Button tabAvailable;

    [Header("详情")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescText;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text hintText;              // "去找铁匠" 提示
    [SerializeField] private Button trackButton;
    [SerializeField] private RectTransform detailContent;

    private string selectedQuestId;
    private int selectedQuestIndex = -1;
    private QuestType currentTab = QuestType.Main;
    private bool showAvailableTab;
    private Sequence detailSeq;
    private Vector2 detailOriginalPos;
    private bool detailPosRecorded;

    private void Awake()
    {
        if (trackButton != null)
            trackButton.onClick.AddListener(OnTrackClicked);

        if (tabMain != null) tabMain.onClick.AddListener(() => SwitchTab(QuestType.Main, false));
        if (tabSide != null) tabSide.onClick.AddListener(() => SwitchTab(QuestType.Side, false));
        if (tabTemporary != null) tabTemporary.onClick.AddListener(() => SwitchTab(QuestType.Temporary, false));
        if (tabAvailable != null) tabAvailable.onClick.AddListener(() => SwitchTab(QuestType.Main, true));
    }

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAccepted += OnQuestEvent;
            QuestManager.Instance.OnQuestStageChanged += OnQuestEvent;
            QuestManager.Instance.OnQuestReadyToClaim += OnQuestEvent;
            QuestManager.Instance.OnQuestClaimed += OnQuestEvent;
            QuestManager.Instance.OnQuestFailed += OnQuestEvent;
            QuestManager.Instance.OnObjectiveUpdated += OnObjectiveUpdated;
            QuestManager.Instance.OnTrackChanged += OnQuestEvent;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAccepted -= OnQuestEvent;
            QuestManager.Instance.OnQuestStageChanged -= OnQuestEvent;
            QuestManager.Instance.OnQuestReadyToClaim -= OnQuestEvent;
            QuestManager.Instance.OnQuestClaimed -= OnQuestEvent;
            QuestManager.Instance.OnQuestFailed -= OnQuestEvent;
            QuestManager.Instance.OnObjectiveUpdated -= OnObjectiveUpdated;
            QuestManager.Instance.OnTrackChanged -= OnQuestEvent;
        }
    }

    private void OnQuestEvent(string _) => Refresh();
    private void OnObjectiveUpdated(string questId, int index, int current)
    {
        if (questId == selectedQuestId) UpdateDetailPanel();
    }

    private void SwitchTab(QuestType type, bool available)
    {
        currentTab = type;
        showAvailableTab = available;
        Refresh();
    }

    // ─── 公开方法 ───

    public void Open() { panelRoot.SetActive(true); Refresh(); }
    public void Close() { panelRoot.SetActive(false); selectedQuestId = null; }
    public void Toggle()
    {
        if (panelRoot.activeSelf) Close(); else Open();
    }

    // ─── 刷新 ───

    private void Refresh()
    {
        selectedQuestId = null;
        RebuildList();
        if (questListContent.childCount > 0)
        {
            var first = questListContent.GetChild(0).GetComponent<Button>();
            if (first != null) first.onClick.Invoke();
        }
        UpdateDetailPanel();
    }

    private void RebuildList()
    {
        foreach (Transform child in questListContent) Destroy(child.gameObject);

        var qm = QuestManager.Instance;
        if (qm == null) return;

        var quests = showAvailableTab ? qm.GetAvailableQuests() : GetQuestListByType(qm, currentTab);

        foreach (var quest in quests)
        {
            if (quest == null) continue;
            var entry = Instantiate(questEntryPrefab, questListContent);
            AudioManager.Instance?.RegisterButton(entry.GetComponent<Button>());

            var text = entry.GetComponentInChildren<TMP_Text>();
            string status = GetQuestStatus(quest.questId);
            string label = showAvailableTab ? $"{quest.questName}" : $"{quest.questName}  {status}";

            if (text != null) text.text = label;

            string capturedId = quest.questId;
            entry.GetComponent<Button>().onClick.AddListener(() => OnEntryClicked(capturedId));

            // 可接取的任务加特殊颜色
            if (showAvailableTab)
            {
                var img = entry.GetComponent<Image>();
                if (img != null) img.color = new Color(0.85f, 0.75f, 0.4f, 0.3f);
            }
        }
    }

    private List<QuestData> GetQuestListByType(QuestManager qm, QuestType type)
    {
        var result = new List<QuestData>();
        foreach (var quest in qm.GetAllQuestData())
        {
            if (quest.questType != type) continue;
            result.Add(quest);
        }
        return result;
    }

    private string GetQuestStatus(string questId)
    {
        var qm = QuestManager.Instance;
        if (qm == null) return "";

        if (qm.IsFailed(questId)) return "<color=red>[失败]</color>";
        if (qm.IsReadyToClaim(questId)) return "<color=#FFD700>[可领取]</color>";
        if (qm.IsActive(questId)) return "<color=#A5D6A7>[进行中]</color>";
        if (qm.IsCompleted(questId)) return "<color=yellow>[已完成]</color>";
        return "";
    }

    // ─── 条目点击 ───

    private void OnEntryClicked(string questId)
    {
        selectedQuestId = questId;
        selectedQuestIndex = -1;
        UpdateDetailPanel();
    }

    // ─── 详情面板 ───

    private void UpdateDetailPanel()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        if (string.IsNullOrEmpty(selectedQuestId) || qm.GetQuestData(selectedQuestId) == null)
        {
            ClearDetail();
            return;
        }

        var quest = qm.GetQuestData(selectedQuestId);
        var stage = qm.GetCurrentStage(selectedQuestId);
        var progress = qm.GetStageProgress(selectedQuestId);

        if (detailNameText != null)
            detailNameText.text = $"{quest.questName}  {GetQuestStatus(selectedQuestId)}";
        if (detailDescText != null)
            detailDescText.text = quest.description;

        // 目标文本
        string objText = "";
        if (stage?.objectives != null)
        {
            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var obj = stage.objectives[i];
                int cur = (progress != null && i < progress.Length) ? progress[i] : 0;
                string action = obj.type switch
                {
                    ObjectiveType.Kill => "击杀",
                    ObjectiveType.Collect => "收集",
                    ObjectiveType.TalkToNPC => "对话",
                    _ => "完成",
                };
                string name = TargetNameResolver.Resolve(obj.type, obj.targetId);
                objText += $"{action} {name}  <color=#FFD700>{cur}</color>/{obj.requiredCount}\n";
            }
        }
        if (objectiveText != null) objectiveText.text = objText.TrimEnd('\n');

        // 阶段信息
        if (hintText != null)
        {
            if (qm.IsReadyToClaim(selectedQuestId))
                hintText.text = $"<color=#FFD700>已完成，找 {quest.triggerNpcId} 领取奖励</color>";
            else if (qm.IsActive(selectedQuestId) && stage != null)
            {
                if (qm.IsCurrentStageComplete(selectedQuestId))
                {
                    string npc = GetTurnInNpcName(quest);
                    hintText.text = $"<color=#A5D6A7>阶段完成，去找 {npc}</color>";
                }
                else
                    hintText.text = "";
            }
            else if (qm.IsCompleted(selectedQuestId))
                hintText.text = "<color=grey>已完成</color>";
            else if (qm.IsFailed(selectedQuestId))
                hintText.text = "<color=red>已失败</color>";
            else
                hintText.text = "";
        }

        // 追踪按钮
        UpdateTrackButton();
    }

    private string GetTurnInNpcName(QuestData quest)
    {
        // 优先使用 turnInNpcId 的配置
        if (!string.IsNullOrEmpty(quest.triggerNpcId)) return quest.triggerNpcId;
        return "相关 NPC";
    }

    private void ClearDetail()
    {
        if (detailNameText != null) detailNameText.text = "";
        if (detailDescText != null) detailDescText.text = "";
        if (objectiveText != null) objectiveText.text = "";
        if (hintText != null) hintText.text = "";
        SetTrackButtonState("", false, false);
    }

    // ─── 追踪 ───

    private void OnTrackClicked()
    {
        if (string.IsNullOrEmpty(selectedQuestId)) return;
        QuestManager.Instance?.ToggleTrack(selectedQuestId);
    }

    private void UpdateTrackButton()
    {
        var qm = QuestManager.Instance;
        if (qm == null || string.IsNullOrEmpty(selectedQuestId))
        {
            SetTrackButtonState("", false, false);
            return;
        }

        bool tracked = qm.IsTracked(selectedQuestId);
        bool active = qm.IsActive(selectedQuestId) || qm.IsReadyToClaim(selectedQuestId);
        SetTrackButtonState("追踪", active, tracked);
    }

    private void SetTrackButtonState(string text, bool interactable, bool tracked)
    {
        if (trackButton == null) return;
        trackButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        trackButton.interactable = interactable;
        var btnText = trackButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null) btnText.text = tracked ? "追踪中" : text;
        var img = trackButton.GetComponent<Image>();
        if (img != null) img.color = tracked ? Color.yellow : Color.white;
    }
}
