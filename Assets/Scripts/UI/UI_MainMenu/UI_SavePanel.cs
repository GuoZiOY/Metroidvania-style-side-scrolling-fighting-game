using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 存档选择面板。用于主菜单的新游戏/读档。
/// 支持 3 个手动槽位，每个显示等级、场景名、游玩时间、存档日期。
/// </summary>
public class UI_SavePanel : MonoBehaviour
{
    #region 槽位 UI 结构

    [Serializable]
    public class SaveSlotUI
    {
        public GameObject root;              // 整个槽位的根对象
        public TextMeshProUGUI slotLabel;    // "存档 1"
        public TextMeshProUGUI levelText;    // "Lv.5"
        public TextMeshProUGUI sceneText;    // "场景名"
        public TextMeshProUGUI playTimeText; // "02:30:15"
        public TextMeshProUGUI dateText;     // "2026-07-21"
        public Button actionBtn;             // 新游戏/加载
        public Button deleteBtn;             // 删除（仅已有存档显示）
        public GameObject emptyGroup;        // 空槽位提示组
        public GameObject infoGroup;         // 存档信息组
    }

    #endregion

    #region 序列化字段

    [Header("槽位列表")]
    [SerializeField] private SaveSlotUI[] slots = new SaveSlotUI[4];

    [Header("面板组件")]
    [SerializeField] private CanvasGroup panelGroup;       // 面板自身 CanvasGroup
    [SerializeField] private TextMeshProUGUI headerText;   // 标题文字
    [SerializeField] private Button backBtn;               // 返回主菜单

    [Header("确认弹窗")]
    [SerializeField] private GameObject confirmDialog;     // 确认弹窗根对象
    [SerializeField] private TextMeshProUGUI confirmMsg;   // 确认提示文字
    [SerializeField] private Button confirmYesBtn;         // 确认按钮
    [SerializeField] private Button confirmNoBtn;          // 取消按钮

    [Header("新游戏起始场景")]
    [SerializeField] private string newGameScene = "level0";

    [Header("动画参数")]
    [SerializeField] private float fadeDuration = 0.25f;

    #endregion

    #region 状态

    public enum PanelMode { NewGame, Load }
    private PanelMode _currentMode;
    private CanvasGroup _cg;
    private List<SaveProfile> _profiles;

    private Action _pendingAction;              // 待确认的操作
    private int _pendingSlot = -1;

    public event Action<int, PanelMode> OnSlotConfirmed;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        for (int i = 0; i < slots.Length; i++)
        {
            int idx = i;
            slots[i].actionBtn.onClick.AddListener(() => OnActionBtn(idx));
            slots[i].deleteBtn.onClick.AddListener(() => OnDeleteBtn(idx));
        }

        backBtn?.onClick.AddListener(Hide);
        confirmYesBtn?.onClick.AddListener(OnConfirmYes);
        confirmNoBtn?.onClick.AddListener(OnConfirmNo);

        gameObject.SetActive(false);
    }

    #endregion

    #region 公开接口

    public void ShowForNewGame()
    {
        _currentMode = PanelMode.NewGame;
        headerText?.SetText("选择存档位");
        Refresh();
        ShowPanel();
    }

    public void ShowForLoad()
    {
        _currentMode = PanelMode.Load;
        headerText?.SetText("选择要加载的存档");
        Refresh();
        ShowPanel();
    }

    public void Hide()
    {
        HideConfirm();
        _cg?.DOKill();
        _cg?.DOFade(0, fadeDuration).OnComplete(() => gameObject.SetActive(false));
    }

    #endregion

    #region 刷新

    private void Refresh()
    {
        _profiles = SaveManager.Instance?.ListProfiles() ?? new List<SaveProfile>();

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            var p = _profiles.Find(x => x.slotIndex == i);
            bool hasData = p != null && !p.isEmpty;

            s.emptyGroup.SetActive(!hasData);
            s.infoGroup.SetActive(hasData);
            s.deleteBtn.gameObject.SetActive(hasData);

            var btnText = s.actionBtn.GetComponentInChildren<TextMeshProUGUI>();

            if (hasData)
            {
                s.levelText?.SetText($"Lv.{p.playerLevel}");
                s.sceneText?.SetText(p.sceneName);
                s.playTimeText?.SetText(FormatPlayTime(p.playTime));
                s.dateText?.SetText(FormatSaveTime(p.saveTime));
                s.actionBtn.interactable = true;

                if (btnText != null)
                    btnText.SetText(_currentMode == PanelMode.Load ? "加载" : "覆盖");
            }
            else
            {
                s.actionBtn.interactable = _currentMode != PanelMode.Load;
                if (btnText != null)
                    btnText.SetText("新游戏");
            }

            s.root.SetActive(true);
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

    #region 按钮事件

    private void OnActionBtn(int index)
    {
        _pendingSlot = index;
        var p = _profiles?.Find(x => x.slotIndex == index);
        bool hasData = p != null && !p.isEmpty;

        switch (_currentMode)
        {
            case PanelMode.NewGame:
                if (hasData)
                    ShowConfirm("确定覆盖此存档吗？\n当前进度将被永久覆盖。",
                        () => StartNewGame(index));
                else
                    StartNewGame(index);
                break;

            case PanelMode.Load:
                if (hasData) LoadGame(index);
                break;
        }
    }

    private void OnDeleteBtn(int index)
    {
        _pendingSlot = index;
        ShowConfirm("确定删除此存档吗？\n此操作不可恢复。",
            () => DeleteSlot(index));
    }

    #endregion

    #region 实际操作

    private void StartNewGame(int index)
    {
        SaveManager.Instance?.Delete(index);
        OnSlotConfirmed?.Invoke(index, PanelMode.NewGame);
        SceneManager.LoadScene(newGameScene);
    }

    private void LoadGame(int index)
    {
        OnSlotConfirmed?.Invoke(index, PanelMode.Load);
        SaveManager.Instance?.Load(index);
    }

    private void DeleteSlot(int index)
    {
        SaveManager.Instance?.Delete(index);
        Refresh();
    }

    #endregion

    #region 确认弹窗

    private void ShowConfirm(string message, Action onConfirm)
    {
        if (confirmDialog == null) { onConfirm?.Invoke(); return; }

        _pendingAction = onConfirm;
        confirmMsg?.SetText(message);
        confirmDialog.SetActive(true);

        var cg = confirmDialog.GetComponent<CanvasGroup>();
        if (cg == null) cg = confirmDialog.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        confirmDialog.transform.localScale = Vector3.one * 0.85f;

        DOTween.Kill(confirmDialog);
        DOTween.Sequence()
            .Join(cg.DOFade(1, 0.15f))
            .Join(confirmDialog.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack));
    }

    private void HideConfirm()
    {
        if (confirmDialog != null)
            confirmDialog.SetActive(false);
        _pendingAction = null;
    }

    private void OnConfirmYes()
    {
        _pendingAction?.Invoke();
        _pendingAction = null;
        _pendingSlot = -1;
        HideConfirm();
    }

    private void OnConfirmNo()
    {
        _pendingAction = null;
        _pendingSlot = -1;
        HideConfirm();
    }

    #endregion

    #region 面板显隐

    private void ShowPanel()
    {
        gameObject.SetActive(true);
        _cg.alpha = 0;
        _cg.DOFade(1, fadeDuration);
    }

    #endregion
}
