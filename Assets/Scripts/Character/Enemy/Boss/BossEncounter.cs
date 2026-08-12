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
    [SerializeField] private Transform spawnPoint;              // Boss 出场点
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
    private const float FIGHT_ZOOM_SIZE = 8f; // 战斗拉远 orthographic size（放大视野）

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

        // ② 相机特写：Intro vcam 拉高
        if (introCam != null)
            introCam.Priority = INTRO_PRIORITY;

        // ③ 实例化 Boss（先隐藏，出场再显）
        boss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation).GetComponent<Boss_SlimeKing>();
        bossHealth = boss.GetComponent<Entity_Health>();
        boss.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.5f);

        // ④ Boss 出场 + 名称横幅 + 切 Boss BGM
        boss.gameObject.SetActive(true);
        AudioManager.Instance?.PushBgm(bossBgm, 0.3f);

        // ⑤ 落地冲击演出（坠入 + 震屏）
        boss.transform.position += Vector3.up * 4f;
        yield return new WaitForSeconds(0.6f);
        screenShake?.ShakeWith(new Vector3(3f, -4f, 0f));

        // ⑥ 进入战斗：血条 + 激活 Boss + 相机拉远 + 解锁输入
        if (healthBar != null) healthBar.BindBoss(bossHealth, boss.enemyName);
        boss.BeginFight();
        if (introCam != null) introCam.Priority = 0; // 镜头回玩家
        ZoomCameraOut();
        GameInput.IsPlayerControlBlocked = false;
        State = EncounterState.Fighting;

        boss.OnLanded += OnBossLanded;
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

    // 战斗相机拉远：放大视野看清落点预告（scaled 时间，暂停冻结）
    private void ZoomCameraOut()
    {
        if (followCam == null) return;
        DOTween.To(() => followCam.m_Lens.OrthographicSize,
            v => followCam.m_Lens.OrthographicSize = v,
            FIGHT_ZOOM_SIZE, 0.5f);
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
