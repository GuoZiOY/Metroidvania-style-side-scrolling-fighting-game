using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>任务追踪 HUD：显示已标记任务的名称和进度</summary>
public class UI_QuestTracker : MonoBehaviour
{
    [Header("追踪显示")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private UnityEngine.UI.Button hideButton;
    [SerializeField] private Color hideButtonActiveColor = Color.yellow;

    private RectTransform _rect;
    private Vector2 _showPos;
    private Tween _slideTween;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _showPos = _rect.anchoredPosition;
    }

    public void ToggleShow()
    {
        _slideTween?.Kill();
        bool show = !gameObject.activeSelf;

        if (show)
        {
            gameObject.SetActive(true);
            // 从右滑入
            _rect.anchoredPosition = _showPos + new Vector2(_rect.rect.width, 0);
            _slideTween = _rect.DOAnchorPos(_showPos, 0.3f).SetEase(Ease.OutCubic);
        }
        else
        {
            // 滑出到右侧，完成后隐藏
            _slideTween = _rect.DOAnchorPos(_showPos + new Vector2(_rect.rect.width, 0), 0.25f)
                .SetEase(Ease.InCubic)
                .OnComplete(() => gameObject.SetActive(false));
        }
        UpdateButtonColor(show);
    }

    private void UpdateButtonColor(bool visible)
    {
        if (hideButton == null) return;
        var img = hideButton.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.color = visible ? hideButtonActiveColor : Color.white;
    }

    private void Start()
    {
        if (hideButton != null)
        {
            hideButton.onClick.AddListener(ToggleShow);
            UpdateButtonColor(gameObject.activeSelf);
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnTrackChanged += OnTrackChanged;
            QuestManager.Instance.OnObjectiveUpdated += OnObjectiveUpdated;
            QuestManager.Instance.OnQuestAccepted += OnQuestEvent;
            QuestManager.Instance.OnQuestReadyToClaim += OnQuestEvent;
            QuestManager.Instance.OnQuestClaimed += OnQuestEvent;
            QuestManager.Instance.OnQuestFailed += OnQuestEvent;
        }
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnTrackChanged -= OnTrackChanged;
            QuestManager.Instance.OnObjectiveUpdated -= OnObjectiveUpdated;
            QuestManager.Instance.OnQuestAccepted -= OnQuestEvent;
            QuestManager.Instance.OnQuestReadyToClaim -= OnQuestEvent;
            QuestManager.Instance.OnQuestClaimed -= OnQuestEvent;
            QuestManager.Instance.OnQuestFailed -= OnQuestEvent;
        }
    }

    private void OnTrackChanged(string _) => RefreshAll();
    private void OnQuestEvent(string _) => RefreshAll();

    private void OnObjectiveUpdated(string questId, int index, int current)
    {
        if (QuestManager.Instance != null && QuestManager.Instance.IsTracked(questId))
            RefreshAll();
    }

    private void RefreshAll()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        var tracked = qm.GetTrackedQuestIds();
        bool hasAny = tracked.Count > 0;

        if (nameText != null)
            nameText.gameObject.SetActive(hasAny);
        if (progressText != null)
            progressText.gameObject.SetActive(hasAny);

        if (!hasAny) return;

        string questId = tracked[0];
        var quest = qm.GetQuestData(questId);
        var progress = qm.GetProgress(questId);

        if (quest == null) return;

        // 任务名：显示名称 + 状态
        if (nameText != null)
        {
            string status = GetStatusText(qm, questId);
            nameText.text = $"{quest.questName} {status}";
        }

        // 任务进度：显示各个目标进度
        if (progressText != null)
        {
            string str = "";
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var obj = quest.objectives[i];
                int current = progress?.objectiveProgress[i] ?? 0;
                string action = obj.type == ObjectiveType.Kill ? "击杀" : "提交";
                string name = TargetNameResolver.Resolve(obj.type, obj.targetId);
                str += $"{action} {name} x{obj.requiredCount} <color=#FFD700>{current}</color>/{obj.requiredCount}";
                if (i < quest.objectives.Count - 1) str += "\n";
            }
            progressText.text = str;
        }
    }

    private string GetStatusText(QuestManager qm, string questId)
    {
        if (qm.IsReadyToClaim(questId))
            return "<color=#FFD700>[可领取]</color>";
        if (qm.IsCompleted(questId))
            return "<color=yellow>[已完成]</color>";
        if (qm.IsFailed(questId))
            return "<color=red>[失败]</color>";
        return "";
    }
}
