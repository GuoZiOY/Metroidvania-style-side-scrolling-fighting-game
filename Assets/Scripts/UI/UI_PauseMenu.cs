using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 暂停菜单 — HUD 菜单按钮呼出：继续游戏 / 返回主菜单 / 退出游戏
// 显隐 + 暂停/恢复由 UIManager.ShowPauseMenu/ClosePauseMenu 统一管理（ModalStack + timeScale）
public class UI_PauseMenu : MonoBehaviour
{
    public static UI_PauseMenu Instance { get; private set; }

    [Header("HUD 菜单按钮（呼出本面板，代码绑定无需 Inspector OnClick）")]
    [SerializeField] private Button menuButton;   // 游戏内 HUD 的"菜单"按钮

    [Header("面板按钮")]
    [SerializeField] private Button continueBtn;   // 继续游戏
    [SerializeField] private Button mainMenuBtn;   // 返回主菜单
    [SerializeField] private Button quitBtn;       // 退出游戏

    private void Awake() // 初始化单例 + 代码绑定按钮（无需手动 Inspector OnClick）
    {
        Instance = this;

        // HUD 菜单按钮 → 打开暂停菜单（由 UIManager 统一处理暂停/入栈/背景）
        if (menuButton != null)
            menuButton.onClick.AddListener(OpenMenu);

        if (continueBtn != null)
            continueBtn.onClick.AddListener(OnContinue);
        if (mainMenuBtn != null)
            mainMenuBtn.onClick.AddListener(OnMainMenu);
        if (quitBtn != null)
            quitBtn.onClick.AddListener(OnQuit);
    }

    // HUD 菜单按钮点击：走 UIManager 统一入口（暂停 + 入栈 + 背景）
    private void OpenMenu()
    {
        UIManager.Instance?.ShowPauseMenu();
    }

    private void OnDestroy() // 清理单例
    {
        if (Instance == this)
            Instance = null;
    }

    // 由 UIManager.ShowPauseMenu 调用：显示面板（暂停已由 UIManager 处理）
    public void Open()
    {
        gameObject.SetActive(true);
    }

    // 由 UIManager.ClosePauseMenu 调用：隐藏面板（恢复已由 UIManager 处理）
    public void Close()
    {
        gameObject.SetActive(false);
    }

    // 继续游戏：关闭菜单（UIManager 恢复 timeScale + 出栈 + 关背景）
    private void OnContinue()
    {
        UIManager.Instance?.ClosePauseMenu();
    }

    // 返回主菜单：先关闭暂停（弹模态+恢复时间+关背景），再清空模态栈。
    // ModalStack 是静态类，若不弹掉暂停模态，跨场景后 level0 重载时 IsGameBlocked 恒真 → 角色无法移动
    private void OnMainMenu()
    {
        UIManager.Instance?.ClosePauseMenu(); // 弹掉"pause"模态 + timeScale=1 + 关背景
        ModalStack.Clear();                   // 防御：清空可能残留的其他模态
        SceneTransitionFader.Instance.TransitionToScene("主菜单"); // 过场黑幕过渡
    }

    // 退出游戏（照抄主菜单 UI_MainMenu.OnQuit）
    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
