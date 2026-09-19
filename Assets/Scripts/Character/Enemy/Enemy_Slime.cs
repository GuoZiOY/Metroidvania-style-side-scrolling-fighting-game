using System.Collections;
using UnityEngine;

// 史莱姆敌人 — 生命脆弱、攻击低的普通敌人（可被反击）
// 分裂机制：母体死亡后随机生成 2~5 个子体，子体缩放为母体缩放的 0.6、生命/伤害平分母体数值，
// 并以抛物线抛出效果散开；落地后锁定角色直接进入战斗。子体不再继续分裂。参数直接在脚本内写死，不暴露额外配置。
public class Enemy_Slime : Enemy
{
    // 分裂参数（硬编码）
    private const int MinSplitCount = 2;               // 分裂数量下限
    private const int MaxSplitCount = 5;               // 分裂数量上限
    private const float ChildScaleMultiplier = 0.6f;   // 子体缩放 = 母体缩放 × 0.6
    private const float SpawnOffsetRadius = 1f;        // 子体生成位置相对母体的散布半径
    private const float MinThrowForce = 9f;            // 抛掷垂直初速度下限（保持原始重力，抛物线更高更利落）
    private const float MaxThrowForce = 13f;           // 抛掷垂直初速度上限
    private const float MaxThrowWaitTime = 2f;         // 抛掷最大等待落地时间（防御性超时）

    [SerializeField] private GameObject slimeChildPrefab; // 子体预制体（未指定时克隆自身）

    private bool hasSplit; // 是否已分裂（子体创建时置 true，保证整条血脉只分裂一次）
    private bool isThrown; // 是否正处于抛掷过程
    private Transform targetPlayer; // 分裂时锁定的角色（子体落地后进入战斗锁定用）

    protected override void Awake() // 实例化状态机（与骷髅一致，动画名通用）
    {
        base.Awake();

        idleState = new Enemy_IdleState(this, stateMachine, "idle");
        moveState = new Enemy_MoveState(this, stateMachine, "move");
        attackState = new Enemy_AttackState(this, stateMachine, "attack");
        battleState = new Enemy_BattleState(this, stateMachine, "battle");
        deadState = new Enemy_DeadState(this, stateMachine, "dead");
        stunnedState = new Enemy_StunnedState(this, stateMachine, "stunned");
    }

    protected override void Start() // 初始化状态机
    {
        base.Start();
        stateMachine.Initialize(idleState);

        // 抛掷中的子体：初始化后立即冻结 AI，避免待机/战斗状态在抛掷期间覆盖速度
        if (isThrown)
            stateMachine.SwitchOffStateMachine();
    }

    // 死亡时触发分裂：母体死亡 → 随机判定分裂数量 → 实例化子体并抛出
    public override void EntityDead()
    {
        // 若抛掷过程中被击杀：先解除状态机冻结，确保死亡状态能正常切换
        if (isThrown)
            stateMachine.canChangeSate = true;

        // 未分裂过才分裂（子体 hasSplit=true，不再分裂）
        if (hasSplit == false)
            Split();

        base.EntityDead();
    }

    // 分裂：随机生成 2~5 个子体，子体缩放 0.6、生命/伤害平分母体、抛物线抛出
    private void Split()
    {
        hasSplit = true; // 标记已分裂，防止重复分裂

        // 母体死亡瞬间处于受击白闪特效中，直接克隆会携带受击材质并使子体永久白闪，
        // 先恢复母体初始材质（并终止其受击/元素特效协程）再生成子体
        if (TryGetComponent<Entity_VFX>(out var motherVfx))
            motherVfx.StopAllVFX();

        int count = Random.Range(MinSplitCount, MaxSplitCount + 1); // 最少2个最多5个

        // 记录母体锁定的角色（供子体落地后直接进入战斗并锁定）
        Transform motherPlayer = GetPlayerReference();

        // 子体来源：优先用子体预制体，未配置时克隆自身（克隆带入的运行时状态由 SetupAsChild 重置）
        GameObject childSource = slimeChildPrefab != null ? slimeChildPrefab : gameObject;

        for (int i = 0; i < count; i++)
        {
            // 在母体周围随机偏移生成子体
            Vector3 spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * SpawnOffsetRadius);

            GameObject childObj = Instantiate(childSource, spawnPos, Quaternion.identity);
            if (childObj.TryGetComponent<Enemy_Slime>(out var child) == false)
            {
                Destroy(childObj);
                continue;
            }

            child.SetupAsChild(this, count, motherPlayer);
        }
    }

    // 配置子体：重置运行时状态 → 记录锁定角色 → 缩放 → 生命/伤害平分 → 抛物线抛出
    private void SetupAsChild(Enemy_Slime mother, int splitCount, Transform lockedPlayer)
    {
        // 子体不再分裂；克隆母体时会带入母体的已分裂标记，统一强制为已分裂
        hasSplit = true;
        isThrown = true; // 标记抛掷中（Start 据此冻结 AI，死亡处理据此解除冻结）
        targetPlayer = lockedPlayer; // 记录母体锁定的角色，落地后直接进入战斗

        // 清除克隆可能带入的死亡标记，并把当前生命同步到新的最大生命
        var health = GetComponent<Entity_Health>();
        if (health != null)
            health.Revive();

        // 子体缩放 = 母体缩放基准 × 0.6（等比缩放）
        float childScale = mother.transform.localScale.x * ChildScaleMultiplier;
        transform.localScale = new Vector3(childScale, childScale, childScale);

        // 生命、伤害平分母体的数值
        float childMaxHP = mother.stats.GetMaxHP() / splitCount;
        float childDamage = mother.stats.GetBasePhyiscalDamage() / splitCount;
        stats.resources.maxHP.SetBaseValue(childMaxHP);
        stats.offense.phyiscalDamage.SetBaseValue(childDamage);

        // 同步当前生命到新的最大生命
        if (health != null)
            health.InitializeHealth();

        // 赋予抛物线抛出初速度
        StartCoroutine(ThrowCo(BuildThrowVelocity()));
    }

    // 计算抛物线抛出初速度（垂直初速 + 随机横向散开；保持原始重力，避免浮空卡入地形）
    private Vector2 BuildThrowVelocity()
    {
        float force = Random.Range(MinThrowForce, MaxThrowForce);      // 抛掷垂直初速度
        float horizontal = force * Random.Range(0.4f, 0.7f) * (Random.value < 0.5f ? -1f : 1f); // 随机左右散开
        return new Vector2(horizontal, force);
    }

    // 抛掷协程：抛掷期间冻结 AI，落地后恢复 AI 并锁定角色进入战斗
    private IEnumerator ThrowCo(Vector2 velocity)
    {
        yield return null; // 等一帧，确保 Start 完成状态机初始化并冻结 AI

        stateMachine.SwitchOffStateMachine(); // 冻结 AI（Start 已冻结，这里兜底）

        rb.linearVelocity = velocity; // 赋予抛物线初速度（保持原始重力，避免浮空卡入地形）

        // 等待落地：离开地面后再次触地视为抛出完成（带超时防御）
        bool leftGround = false;
        float timeout = MaxThrowWaitTime;
        while (timeout > 0f)
        {
            if (IsDead)
                break; // 抛掷中死亡：跳过恢复 AI 的收尾

            if (isOnGround == false)
                leftGround = true;
            else if (leftGround)
                break;

            yield return null;
            timeout -= Time.deltaTime;
        }

        // 收尾：清零速度（死亡时同样清零，避免尸体异常）
        rb.linearVelocity = Vector2.zero;
        isThrown = false;

        // 存活才恢复 AI
        if (IsDead == false)
        {
            stateMachine.canChangeSate = true;

            // 子体落地后对角色有仇恨：锁定分裂时记录的角色（未记录则重新检测），直接进入战斗
            Transform target = targetPlayer != null ? targetPlayer : GetPlayerReference();
            if (target != null)
                TryEnterBattleState(target);
            else
                stateMachine.ChangeState(idleState);
        }
    }
}
