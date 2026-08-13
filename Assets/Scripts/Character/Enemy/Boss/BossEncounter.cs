using System;
using System.Collections;
using UnityEngine;

// 公共 Boss 编排（极简版，v4）——锁门→BGM→血条→开战→击杀/死亡
// 无任何入场演出；玩家进房立刻能动、Boss 直接开打
public class BossEncounter : MonoBehaviour
{
    public enum EncounterState { Idle, Fighting, Victory }

    [Header("引用（场景接线）")]
    [SerializeField] private GameObject bossPrefab;             // Boss 预制体
    [SerializeField] private Transform spawnPoint;              // Boss 生成位置
    [SerializeField] private Portal arrivalPortal;              // 到达传送门（锁定）
    [SerializeField] private GameObject exitPortal;             // 出口传送门（胜利激活）
    [SerializeField] private UI_BossHealthBar healthBar;        // 顶部血条

    public EncounterState State { get; private set; } = EncounterState.Idle;
    public event Action VictoryEvent; // 奖励挂载点

    private Boss_SlimeKing boss;
    private Entity_Health bossHealth;
    private bool defeatedFlag;

    private void Awake()
    {
        if (spawnPoint == null)
            spawnPoint = transform;
    }

    private void Start()
    {
        if (healthBar == null)
            healthBar = FindAnyObjectByType<UI_BossHealthBar>(FindObjectsInactive.Include);
        StartCoroutine(StartFight());
    }

    private void OnDestroy()
    {
        AudioManager.Instance?.PopBgm(0f); // 防 BGM 跨场景残留
    }

    private IEnumerator StartFight()
    {
        State = EncounterState.Fighting;

        // 锁到达传送门（不能回头）
        if (arrivalPortal != null)
            arrivalPortal.SetLocked(true);

        // 切 Boss BGM（曲目集中配置在 AudioManager，这里只触发开战）
        AudioManager.Instance?.PushBossBgm(0.5f);

        // 生成 Boss + 绑血条 + 开战
        boss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation).GetComponent<Boss_SlimeKing>();
        bossHealth = boss.GetComponent<Entity_Health>();
        if (healthBar != null)
        {
            healthBar.BindBoss(bossHealth, boss.enemyName);
            // 分裂后主实例晋升 → 血条重绑到存活者（多体管理）
            boss.OnPrimaryChanged += newPrimary =>
            {
                var hp = newPrimary != null ? newPrimary.GetComponent<Entity_Health>() : null;
                if (hp != null && healthBar != null)
                    healthBar.BindBoss(hp, newPrimary.enemyName);
            };
        }
        boss.BeginFight();

        // 监听 Boss 死亡（最后实例死亡→胜利；玩家死亡由现有死亡系统处理）
        while (State == EncounterState.Fighting)
        {
            if (FindAnyObjectByType<Boss_SlimeKing>() == null)
            {
                OnBossDead();
                break;
            }
            yield return null;
        }
    }

    private void OnBossDead()
    {
        State = EncounterState.Victory;
        if (healthBar != null)
            healthBar.Unbind();
        AudioManager.Instance?.PopBgm(0.8f);
        if (exitPortal != null)
            exitPortal.SetActive(true); // 出口传送门出现
        defeatedFlag = true;
        VictoryEvent?.Invoke();
    }
}
