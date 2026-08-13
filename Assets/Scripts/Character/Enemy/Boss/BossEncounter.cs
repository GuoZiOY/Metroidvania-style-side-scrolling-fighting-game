using System;
using System.Collections;
using UnityEngine;

// 公共 Boss 编排（独立 Boss 房场景版）——场景加载自动开场，不关心 Boss 具体招式
// 流程：传送门进入 → 锁输入 → BGM → 黑幕+Boss名 → Boss大跳进场(震屏) → 血条 → 开战 → 击杀激活出口传送门
// 相机固定不跟随（ortho 由场景 vcam 决定）；死亡走现有死亡面板+读档；OnDestroy 恢复 BGM 防残留
public class BossEncounter : MonoBehaviour
{
    public enum EncounterState { Idle, Intro, Fighting, Victory }

    [Header("引用（场景接线）")]
    [SerializeField] private GameObject bossPrefab;             // Boss 预制体
    [SerializeField] private Transform spawnPoint;              // Boss 战斗落点（跳进场后停在这里）
    [SerializeField] private Transform entryPoint;              // Boss 大跳进场起点（远处/场外）
    [SerializeField] private Portal arrivalPortal;              // 到达传送门（Intro 锁定）
    [SerializeField] private GameObject exitPortal;             // 出口传送门（胜利激活，指向城镇）
    [SerializeField] private AudioClip bossBgm;                 // Boss 曲（calm 段）
    [SerializeField] private AudioClip bossBgmRage;             // 狂暴段（可空，HP<30% 切）
    [SerializeField] private UI_BossHealthBar healthBar;        // 顶部大血条
    [SerializeField] private CinemaScreenShake screenShake;     // 震屏
    [SerializeField] private CanvasGroup blackScreen;           // 黑幕（开场淡入淡出，盖住 Boss 出场）
    [SerializeField] private CanvasGroup bossNameText;          // 屏幕中央 Boss 名（开场淡出）

    public EncounterState State { get; private set; } = EncounterState.Idle;
    public event Action VictoryEvent; // 击杀事件（奖励挂载点，探针可订阅验证）

    private Boss_SlimeKing boss;
    private Entity_Health bossHealth;
    private bool defeatedFlag;

    private void Start()
    {
        // 血条/黑幕/Boss名 在场景内，运行时查找兜底
        if (healthBar == null)
            healthBar = FindAnyObjectByType<UI_BossHealthBar>(FindObjectsInactive.Include);
        // 场景加载自动开场（玩家经传送门到达、PlayerSpawner 定位后）
        StartCoroutine(RunIntro());
    }

    private void OnDestroy()
    {
        // 防 Boss 曲跨场景残留（死亡读档/离开场景）
        AudioManager.Instance?.PopBgm(0f);
    }

    // ==================== Intro ====================

    private IEnumerator RunIntro()
    {
        State = EncounterState.Intro;
        GameInput.IsPlayerControlBlocked = true; // 锁玩家输入（黑幕/Boss名/Boss出场全程）

        // ① 锁到达传送门（禁交互）
        if (arrivalPortal != null)
            arrivalPortal.SetLocked(true);

        // ② 切 Boss BGM
        AudioManager.Instance?.PushBgm(bossBgm, 0.5f);

        // ③ 黑幕淡入（盖住 Boss 出场）
        yield return FadeScreen(blackScreen, true, 0.5f);

        // ④ 屏幕中央 Boss 名淡出（黑幕上）
        if (bossNameText != null)
        {
            bossNameText.alpha = 0f;
            yield return FadeScreen(bossNameText, true, 0.5f);
            yield return new WaitForSeconds(0.8f); // 停留展示
            yield return FadeScreen(bossNameText, false, 0.4f); // 名淡出
        }

        // ⑤ Boss 大跳进场（黑幕遮着，相机不跟随；落地震屏）
        boss = Instantiate(bossPrefab, entryPoint.position, entryPoint.rotation).GetComponent<Boss_SlimeKing>();
        bossHealth = boss.GetComponent<Entity_Health>();
        boss.gameObject.SetActive(true);
        boss.OnLanded += OnBossLanded; // 落地震屏
        boss.entryJumpDone = false;
        StartCoroutine(boss.EntryJump(spawnPoint.position));
        float introJumpTimeout = 5f; // 看门狗：大跳异常卡住也强制放行，防开场卡死
        while (introJumpTimeout > 0f && boss.entryJumpDone == false && boss.IsDead == false)
        {
            yield return null;
            introJumpTimeout -= Time.unscaledDeltaTime; // 真实时间，不受 timeScale 影响
        }

        // ⑥ 黑幕淡出 → 露出 Boss → 血条出现 → 解锁开战
        yield return FadeScreen(blackScreen, false, 0.5f);
        if (healthBar != null)
            healthBar.BindBoss(bossHealth, boss.enemyName);
        boss.BeginFight();
        GameInput.IsPlayerControlBlocked = false;
        State = EncounterState.Fighting;

        StartCoroutine(WatchForRage());

        // 监听 Boss 死亡（玩家死亡由现有死亡系统处理，不在此监听）
        while (State == EncounterState.Fighting)
        {
            if (bossHealth != null && bossHealth.GetCurrentHP() <= 0f)
            {
                OnBossDead();
                break;
            }
            yield return null;
        }
    }

    // CanvasGroup 淡入淡出（手动 lerp，scaled 时间暂停冻结）
    private IEnumerator FadeScreen(CanvasGroup cg, bool show, float duration)
    {
        if (cg == null) yield break;
        float from = cg.alpha;
        float to = show ? 1f : 0f;
        float t = 0f;
        while (t < duration)
        {
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        cg.alpha = to;
    }

    private IEnumerator WatchForRage()
    {
        while (State == EncounterState.Fighting && bossHealth != null && bossBgmRage != null)
        {
            if (bossHealth.GetHealthPercent() < 0.3f)
            {
                AudioManager.Instance?.PushBgm(bossBgmRage, 0.3f);
                yield break;
            }
            yield return null;
        }
    }

    private void OnBossLanded() => screenShake?.ShakeWith(new Vector3(3f, -4f, 0f));

    // ==================== Victory ====================

    private void OnBossDead()
    {
        State = EncounterState.Victory;
        if (boss != null) boss.OnLanded -= OnBossLanded;
        if (healthBar != null) healthBar.Unbind(); // 血条隐藏（OnEntityDead 时序内）
        AudioManager.Instance?.PopBgm(0.8f);
        if (exitPortal != null) exitPortal.SetActive(true); // 出口传送门出现（指向城镇）
        defeatedFlag = true;
        VictoryEvent?.Invoke(); // 奖励挂载点（探针可订阅验证）
    }
}
