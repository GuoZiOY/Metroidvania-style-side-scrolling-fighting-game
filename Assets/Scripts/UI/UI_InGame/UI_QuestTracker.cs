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

        string trackedId = qm.GetTrackedQuestId();
        bool hasAny = !string.IsNullOrEmpty(trackedId);

        if (nameText != null)
            nameText.gameObject.SetActive(hasAny);
        if (progressText != null)
            progressText.gameObject.SetActive(hasAny);

        if (!hasAny) return;

        var quest = qm.GetQuestData(trackedId);
        if (quest == null) return;

        // 任务名：显示名称 + 状态
        if (nameText != null)
        {
            string status = GetStatusText(qm, trackedId);
            nameText.text = $"{quest.questName} {status}";
        }

        // 任务进度：显示当前阶段的各个目标进度
        if (progressText != null)
        {
            var stage = qm.GetCurrentStage(trackedId);
            var stageProgress = qm.GetStageProgress(trackedId);

            string str = "";
            if (stage?.objectives != null)
            {
                for (int i = 0; i < stage.objectives.Count; i++)
                {
                    var obj = stage.objectives[i];
                    int current = (stageProgress != null && i < stageProgress.Length) ? stageProgress[i] : 0;
                    string action = obj.type switch
                    {
                        ObjectiveType.Kill => "击杀",
                        ObjectiveType.Collect => "提交",
                        ObjectiveType.TalkToNPC => "对话",
                        _ => "完成",
                    };
                    string name = TargetNameResolver.Resolve(obj.type, obj.targetId);
                    str += $"{action} {name}  <color=#FFD700>{current}</color>/{obj.requiredCount}\n";
                }
            }
            progressText.text = str.TrimEnd('\n');
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
