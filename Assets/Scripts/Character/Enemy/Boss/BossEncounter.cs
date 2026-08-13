using System;
using System.Collections;
using UnityEngine;
using Cinemachine;
using DG.Tweening;

// 公共 Boss 编排（独立 Boss 房场景版）——场景加载自动开场，不关心 Boss 具体招式
// 流程：传送门进入 → 锁到达传送门 → 相机特写+BGM+血条 → 战斗（相机拉远）→ 击杀激活出口传送门
// 死亡走现有死亡面板+读档（场景卸载重置 Boss），本组件不处理；OnDestroy 恢复 BGM 防残留
public class BossEncounter : MonoBehaviour
{
    public enum EncounterState { Idle, Intro, Fighting, Victory }

    [Header("引用（场景接线）")]
    [SerializeField] private GameObject bossPrefab;             // Boss 预制体
    [SerializeField] private Transform spawnPoint;              // Boss 战斗落点（跳进场后停在这里）
    [SerializeField] private Transform entryPoint;              // Boss 大跳进场起点（远处/场外）
    [SerializeField] private Portal arrivalPortal;              // 到达传送门（Intro 锁定）
    [SerializeField] private GameObject exitPortal;             // 出口传送门（胜利激活，指向城镇）
    [SerializeField] private CinemachineVirtualCamera followCam; // 场景 follow vcam（战斗拉远）
    [SerializeField] private CinemachineVirtualCamera introCam;  // Intro vcam（出场特写）
    [SerializeField] private AudioClip bossBgm;                 // Boss 曲（calm 段）
    [SerializeField] private AudioClip bossBgmRage;             // 狂暴段（可空，HP<30% 切）
    [SerializeField] private UI_BossHealthBar healthBar;        // 顶部大血条
    [SerializeField] private BossMinionRegistry minionRegistry; // 迷你王登记处
    [SerializeField] private CinemaScreenShake screenShake;     // 震屏

    private const int INTRO_PRIORITY = 20;   // Intro vcam 拉高优先级（follow 默认 9）

    public EncounterState State { get; private set; } = EncounterState.Idle;
    public event Action VictoryEvent; // 击杀事件（奖励挂载点，探针可订阅验证）

    private Boss_SlimeKing boss;
    private Entity_Health bossHealth;
    private float originalOrthoSize = 5f;
    private bool defeatedFlag;

    private void Awake()
    {
        if (minionRegistry == null) minionRegistry = GetComponentInChildren<BossMinionRegistry>();
        if (followCam != null) originalOrthoSize = followCam.m_Lens.OrthographicSize;
    }

    private void Start()
    {
        // 血条在持久 HUD（跨场景 DontDestroyOnLoad），场景内接不了引用，运行时查找
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
        GameInput.IsPlayerControlBlocked = true; // 锁玩家输入

        // ① 锁到达传送门（禁交互）
        if (arrivalPortal != null)
            arrivalPortal.SetLocked(true);

        // ② 切 Boss BGM（落地冲击帧为遮罩起点）
        AudioManager.Instance?.PushBgm(bossBgm, 0.3f);

        // ③ Boss 在远处入场点生成（大跳进场的起点）
        boss = Instantiate(bossPrefab, entryPoint.position, entryPoint.rotation).GetComponent<Boss_SlimeKing>();
        bossHealth = boss.GetComponent<Entity_Health>();
        boss.gameObject.SetActive(true);
        boss.OnLanded += OnBossLanded; // 落地震屏

        // ④ Intro 相机跟随 Boss（大跳进场镜头）：Follow=LookAt=Boss
        if (introCam != null)
        {
            introCam.Follow = boss.transform;
            introCam.LookAt = boss.transform;
            introCam.Priority = INTRO_PRIORITY;
        }

        // ⑤ Boss 大跳进场：从远处弧线跳向战斗落点，镜头跟着它
        // 看门狗：即使大跳因异常卡住（如 timeScale=0 时 WaitForSeconds/Time.deltaTime 停摆），
        // 也强制超时放行，保证相机一定回到玩家、战斗一定开始
        boss.entryJumpDone = false;
        StartCoroutine(boss.EntryJump(spawnPoint.position));
        float introJumpTimeout = 5f;
        while (introJumpTimeout > 0f && boss.entryJumpDone == false && boss.IsDead == false)
        {
            yield return null;
            introJumpTimeout -= Time.unscaledDeltaTime; // 真实时间，不受 timeScale 影响
        }

        // ⑥ 落地 → 切回玩家相机 → 激活战斗 + 拉远视野
        if (introCam != null)
            introCam.Priority = 0; // 镜头 blend 回玩家
        if (healthBar != null)
            healthBar.BindBoss(bossHealth, boss.enemyName);
        boss.BeginFight();
        ZoomCameraOut();
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

    // 战斗相机拉远：放大视野看清落点预告（按原值 ×1.6，保证"拉远"而不是反向；scaled 时间，暂停冻结）
    private void ZoomCameraOut()
    {
        if (followCam == null) return;
        DOTween.To(() => followCam.m_Lens.OrthographicSize,
            v => followCam.m_Lens.OrthographicSize = v,
            originalOrthoSize * 1.6f, 0.5f);
    }

    private void RestoreCamera()
    {
        if (followCam == null) return;
        DOTween.To(() => followCam.m_Lens.OrthographicSize,
            v => followCam.m_Lens.OrthographicSize = v,
            originalOrthoSize, 0.5f);
    }

    // ==================== Victory ====================

    private void OnBossDead()
    {
        State = EncounterState.Victory;
        if (boss != null) boss.OnLanded -= OnBossLanded;
        if (healthBar != null) healthBar.Unbind(); // 血条隐藏（OnEntityDead 时序内）
        if (minionRegistry != null) minionRegistry.Clear(); // 清迷你王
        RestoreCamera();
        AudioManager.Instance?.PopBgm(0.8f);
        if (exitPortal != null) exitPortal.SetActive(true); // 出口传送门出现（指向城镇）
        defeatedFlag = true;
        VictoryEvent?.Invoke(); // 奖励挂载点（探针可订阅验证）
    }
}
