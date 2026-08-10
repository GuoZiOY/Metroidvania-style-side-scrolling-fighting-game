using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 存档选择面板。槽位使用 SaveSlot 预制体，自动收集组件。
/// </summary>
public class UI_SavePanel : MonoBehaviour
{
    [Header("槽位列表（拖入 SaveSlot 预制体实例）")]
    [SerializeField] private SaveSlot[] slots = new SaveSlot[4];

    [Header("面板组件")]
    [SerializeField] private TextMeshProUGUI headerText;   // "选择存档"

    [Header("公共操作按钮")]
    [SerializeField] private Button actionBtn;
    [SerializeField] private TextMeshProUGUI actionBtnText;
    [SerializeField] private Button deleteBtn;

    [Header("确认弹窗")]
    [SerializeField] private GameObject confirmDialog;
    [SerializeField] private TextMeshProUGUI confirmMsg;
    [SerializeField] private Button confirmYesBtn;
    [SerializeField] private Button confirmNoBtn;

    [Header("新游戏起始场景")]
    [SerializeField] private string newGameScene = "level0";

    [Header("动画")]
    [SerializeField] private float fadeDuration = 0.25f;

    private CanvasGroup cg;
    private List<SaveProfile> profiles;
    private int selectedSlot = -1;
    private Action pendingAction;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        for (int i = 0; i < slots.Length; i++)
        {
            int idx = i;
            if (slots[i] != null)
                slots[i].clickBtn?.onClick.AddListener(() => SelectSlot(idx));
        }

        actionBtn?.onClick.AddListener(OnAction);
        deleteBtn?.onClick.AddListener(OnDelete);
        confirmYesBtn?.onClick.AddListener(OnConfirmYes);
        confirmNoBtn?.onClick.AddListener(OnConfirmNo);

        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Refresh();
        SelectSlot(-1);
    }

    #region 刷新

    private void Refresh()
    {
        profiles = SaveManager.Instance?.ListProfiles() ?? new List<SaveProfile>();

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null) continue;

            var p = profiles.Find(x => x.slotIndex == i);
            bool hasData = p != null && !p.isEmpty;

            s.emptyGroup?.SetActive(!hasData);
            s.infoGroup?.SetActive(hasData);

            if (hasData)
            {
                s.levelText?.SetText($"等级: {p.playerLevel}");
                s.sceneText?.SetText($"地点: {p.sceneName}");
                s.playTimeText?.SetText($"游玩时间: {FormatPlayTime(p.playTime)}");
                s.dateText?.SetText($"保存日期: {FormatSaveTime(p.saveTime)}");
            }

            s.gameObject.SetActive(true);
        }
    }

    private static string FormatPlayTime(float seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes:D2}:{t.Seconds:D2}";
    }

    private static string FormatSaveTime(string saveTime)
    {
        if (string.IsNullOrEmpty(saveTime)) return "";
        try
        {
            DateTime dt = DateTime.Parse(saveTime, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
        catch { return saveTime; }
    }

    #endregion

    #region 选中

    private void SelectSlot(int index)
    {
        selectedSlot = index;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i]?.highlight == null) continue;
            slots[i].highlight.SetActive(i != index);
        }

        bool selected = index >= 0;
        bool hasData = selected && HasData(index);

        actionBtn.interactable = selected;
        deleteBtn.interactable = selected && hasData;

        if (!selected)
            actionBtnText?.SetText("选择存档");
        else if (hasData)
            actionBtnText?.SetText("加载存档");
        else
            actionBtnText?.SetText("开始新游戏");
    }

    private bool HasData(int index) => profiles?.Exists(x => x.slotIndex == index && !x.isEmpty) ?? false;

    #endregion

    #region 按钮

    private void OnAction()
    {
        if (selectedSlot < 0) return;
        if (HasData(selectedSlot))
            SaveManager.Instance?.Load(selectedSlot);
        else
        {
            // 记录当前槽位，并清空旧数据
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Delete(selectedSlot);
                SaveManager.Instance.CurrentSlotIndex = selectedSlot;
            }
            // 新游戏：到达后把玩家生成在场景入口存档点（isEntryPoint），过场黑幕过渡
            PlayerSpawner.MarkSpawnAtEntry(false);
            SceneTransitionFader.Instance.TransitionToScene(newGameScene);
        }
    }

    private void OnDelete()
    {
        if (selectedSlot < 0 || !HasData(selectedSlot)) return;
        ShowConfirm("确定删除此存档吗？\n此操作不可恢复。", () =>
        {
            SaveManager.Instance?.Delete(selectedSlot);
            Refresh();
            SelectSlot(-1);
        });
    }

    #endregion

    #region 确认弹窗

    private void ShowConfirm(string message, Action onConfirm)
    {
        if (confirmDialog == null) { onConfirm?.Invoke(); return; }
        pendingAction = onConfirm;
        confirmMsg?.SetText(message);
        confirmDialog.SetActive(true);

        var cg = confirmDialog.GetComponent<CanvasGroup>();
        if (cg == null) cg = confirmDialog.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        confirmDialog.transform.localScale = Vector3.one * 0.85f;
        DOTween.Kill(confirmDialog);
        // SetUpdate(true)：主菜单 timeScale=0 时普通 tween 不推进，会导致 alpha 卡 0 弹窗不可见
        DOTween.Sequence()
            .SetUpdate(true)
            .Join(cg.DOFade(1, 0.15f))
            .Join(confirmDialog.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack));
    }

    private void HideConfirm() { if (confirmDialog != null) confirmDialog.SetActive(false); pendingAction = null; }
    private void OnConfirmYes() { pendingAction?.Invoke(); pendingAction = null; HideConfirm(); }
    private void OnConfirmNo() { pendingAction = null; HideConfirm(); }

    #endregion
}
