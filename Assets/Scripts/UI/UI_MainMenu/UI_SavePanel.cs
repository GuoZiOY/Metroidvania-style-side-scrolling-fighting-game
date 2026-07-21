using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 存档选择面板。用于主菜单的新游戏/读档。
/// 4 个槽位只做信息展示和选中，操作通过两个公共按钮进行。
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
        public GameObject emptyGroup;        // 空槽位提示组
        public GameObject infoGroup;         // 存档信息组
        public GameObject highlight;         // 选中高亮（选中时显示）
        public Button clickBtn;              // 点击选中该槽位
    }

    #endregion

    #region 序列化字段

    [Header("槽位列表")]
    [SerializeField] private SaveSlotUI[] slots = new SaveSlotUI[4];

    [Header("面板组件")]
    [SerializeField] private CanvasGroup panelGroup;       // 面板自身 CanvasGroup
    [SerializeField] private TextMeshProUGUI headerText;   // 标题文字
    [SerializeField] private Button backBtn;               // 返回主菜单

    [Header("公共操作按钮")]
    [SerializeField] private Button actionBtn;              // 新游戏/覆盖/加载
    [SerializeField] private TextMeshProUGUI actionBtnText;
    [SerializeField] private Button deleteBtn;              // 删除存档

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

    private int _selectedSlot = -1;     // 当前选中的槽位，-1=未选
    private Action _pendingAction;      // 待确认的操作

    public event Action<int, PanelMode> OnSlotConfirmed;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        // 每个槽位可点击选中
        for (int i = 0; i < slots.Length; i++)
        {
            int idx = i;
            slots[i].clickBtn?.onClick.AddListener(() => SelectSlot(idx));
        }

        backBtn?.onClick.AddListener(Hide);
        actionBtn?.onClick.AddListener(OnAction);
        deleteBtn?.onClick.AddListener(OnDelete);
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

            if (hasData)
            {
                s.levelText?.SetText($"Lv.{p.playerLevel}");
                s.sceneText?.SetText(p.sceneName);
                s.playTimeText?.SetText(FormatPlayTime(p.playTime));
                s.dateText?.SetText(FormatSaveTime(p.saveTime));
            }

            s.root.SetActive(true);
        }

        // 清除选中状态
        SelectSlot(-1);
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

    #region 选中逻辑

    private void SelectSlot(int index)
    {
        _selectedSlot = index;

        // 高亮切换
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].highlight != null)
                slots[i].highlight.SetActive(i == index);
        }

        // 公共按钮状态
        bool selected = index >= 0;
        actionBtn.interactable = selected;
        deleteBtn.interactable = selected && HasData(index);
        actionBtnText?.SetText(GetActionBtnText(index));
    }

    private bool HasData(int index)
    {
        if (index < 0) return false;
        var p = _profiles?.Find(x => x.slotIndex == index);
        return p != null && !p.isEmpty;
    }

    private string GetActionBtnText(int index)
    {
        if (index < 0) return "选择存档";
        bool hasData = HasData(index);

        switch (_currentMode)
        {
            case PanelMode.NewGame:
                return hasData ? "覆盖并开始新游戏" : "开始新游戏";
            case PanelMode.Load:
                return hasData ? "加载存档" : "所选槽位无数据";
            default:
                return "确定";
        }
    }

    #endregion

    #region 按钮事件

    private void OnAction()
    {
        if (_selectedSlot < 0) return;
        bool hasData = HasData(_selectedSlot);

        switch (_currentMode)
        {
            case PanelMode.NewGame:
                if (hasData)
                    ShowConfirm("确定覆盖此存档吗？\n当前进度将被永久覆盖。",
                        () => StartNewGame(_selectedSlot));
                else
                    StartNewGame(_selectedSlot);
                break;

            case PanelMode.Load:
                if (hasData) LoadGame(_selectedSlot);
                break;
        }
    }

    private void OnDelete()
    {
        if (_selectedSlot < 0 || !HasData(_selectedSlot)) return;
        ShowConfirm("确定删除此存档吗？\n此操作不可恢复。",
            () => DeleteSlot(_selectedSlot));
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
        HideConfirm();
    }

    private void OnConfirmNo()
    {
        _pendingAction = null;
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
