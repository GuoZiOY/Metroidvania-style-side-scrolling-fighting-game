using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单控制器。面板切换全由 PanelSwitcher 处理。
/// </summary>
public class UI_MainMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button quitBtn;

    private void Start()
    {
        UI_ButtonEffect.HookAll();

        // 兜底：进主菜单清空模态栈（ModalStack 是静态类跨场景残留，
        // 若游戏场景留有未弹出的模态，读档继续后 GameInput.IsGameBlocked 会屏蔽角色输入）
        ModalStack.Clear();

        if (titleText != null)
        {
            titleText.transform.localScale = Vector3.one * 0.8f;
            titleText.alpha = 0;
            DOTween.Sequence()
                .Join(titleText.transform.DOScale(Vector3.one, 0.6f).SetEase(Ease.OutBack))
                .Join(titleText.DOFade(1, 0.4f));
        }

        quitBtn?.onClick.AddListener(OnQuit);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
