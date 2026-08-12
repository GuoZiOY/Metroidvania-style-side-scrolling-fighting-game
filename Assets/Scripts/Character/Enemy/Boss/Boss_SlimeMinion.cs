using UnityEngine;

// 迷你史莱姆王——简单跳向玩家 + 接触伤害；不分裂不召唤（独立小怪，不复用 Enemy_Slime）
public class Boss_SlimeMinion : Enemy
{
    [SerializeField] private float hopInterval = 1.2f;   // 跳跃间隔（秒）
    [SerializeField] private float hopForce = 6f;        // 跳跃力度（y 初速）
    [SerializeField] private float hopHorizontalRatio = 0.5f; // 横向速度 = hopForce × 此值

    private BossMinionRegistry registry; // 所属登记处（Boss 召唤时注入）
    private float nextHopTime;           // 下次跳跃时间
    private bool hopAIEnabled;           // 是否激活跳跃 AI

    protected override void Awake()
    {
        base.Awake();
        idleState = new Enemy_IdleState(this, stateMachine, "idle");
        moveState = new Enemy_MoveState(this, stateMachine, "move");
        attackState = new Enemy_AttackState(this, stateMachine, "attack");
        battleState = new Enemy_BattleState(this, stateMachine, "battle");
        deadState = new Enemy_DeadState(this, stateMachine, "dead");
        stunnedState = new Enemy_StunnedState(this, stateMachine, "stunned");
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);
        stateMachine.SwitchOffStateMachine(); // 冻结 FSM（迷你王用自定义跳跃 AI，不用 FSM 追击）
        hopAIEnabled = true;
    }

    // Boss 召唤时注入登记处；登记后死亡注销
    public void InitMinion(BossMinionRegistry reg)
    {
        registry = reg;
        registry?.Register(this);

        nextHopTime = Time.time + hopInterval; // 出生后先等一个间隔再跳（消除出生瞬跳）
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead || hopAIEnabled == false)
            return;
        if (Time.time < nextHopTime)
            return;
        if (isOnGround == false)
            return; // 空中不施加跳跃（防被击飞时延长滞空）

        // 每 hopInterval 朝玩家方向跳一下
        Transform target = player;
        if (target == null)
            target = GetPlayerReference();
        if (target != null)
        {
            int dir = target.position.x > transform.position.x ? 1 : -1;
            HandleFlip(dir);
            rb.linearVelocity = new Vector2(dir * hopForce * hopHorizontalRatio, hopForce);
        }
        nextHopTime = Time.time + hopInterval;
    }

    public override void EntityDead()
    {
        registry?.Unregister(this);        // 死亡注销，释放召唤上限
        stateMachine.canChangeSate = true; // 解除 FSM 冻结，让死亡状态可切换（同 Enemy_Slime 抛掷子体处理）
        base.EntityDead();
    }
}
