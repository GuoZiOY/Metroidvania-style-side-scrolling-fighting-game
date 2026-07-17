using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyArea : AreaDetectorBase // 敌人区域：管理敌人生成和难度系统
{
    #region 序列化字段

    [Header("区域设置")]
    [SerializeField] private AreaDifficulty areaDifficulty = AreaDifficulty.Normal; // 区域难度
    [SerializeField] private int baseEnemyLevel = 1; // 基础敌人等级
    [SerializeField] private float eliteSpawnChance = 0.1f; // 精英生成概率
    [SerializeField] private int enemyCount = 3; // 敌人数

    [Header("敌人生成")]
    [SerializeField] private bool spawnOnEnter = true; // 进入时是否生成
    [SerializeField] private bool allowRespawn = true; // 是否允许重生（允许多次生成）
    [SerializeField] private float spawnStartDelay = 0.5f; // 开始生成前的延迟
    [SerializeField] private float spawnInterval = 1f; // 每个敌人生成的间隔
    [SerializeField] private float respawnCooldown = 10f; // 离开后重新生成的冷却时间（秒）

    [Header("组件引用")]
    [SerializeField] private EnemySpawner enemySpawner; // 敌人生成器引用

    #endregion

    #region 私有字段

    private bool hasSpawned; // 是否已生成过
    private float lastSpawnTime; // 上次生成时间
    private bool hasFirstEntered; // 是否首次进入
    private float lastInAreaRespawnTime; // 上次在区域内重生的时间

    #endregion

    #region 区域内重生逻辑

    private void CheckInAreaRespawn() // 检查是否需要在区域内重生
    {
        // 检查是否所有敌人都被击杀
        if (enemySpawner != null && enemySpawner.GetAliveEnemyCount() == 0)
        {
            // 检查是否达到重生间隔
            if (Time.time - lastInAreaRespawnTime >= respawnCooldown)
            {
                RespawnEnemiesInArea();
            }
        }
    }

    private void RespawnEnemiesInArea() // 在区域内重生敌人
    {
        if (enemySpawner != null)
        {
            // 在区域内重生时不清除已存在的敌人（继续生成）
            enemySpawner.SpawnEnemies(enemyCount, eliteSpawnChance, areaDifficulty, baseEnemyLevel, spawnStartDelay, spawnInterval, false);
            lastInAreaRespawnTime = Time.time;
            Debug.Log($"[{areaName}] 在区域内重生 {enemyCount} 个敌人");
        }
    }

    #endregion




    #region 事件

    public event Action OnEnemyKilledEvent; // 敌人死亡事件
    public event Action OnAreaClearedEvent; // 区域清空事件

    #endregion

    #region 生命周期

    protected override void Awake() // 初始化
    {
        base.Awake();

        // 自动获取敌人生成器组件
        if (enemySpawner == null)
        {
            enemySpawner = GetComponent<EnemySpawner>();
        }
        // 订阅敌人生成器事件
        if (enemySpawner != null)
        {
            enemySpawner.OnEnemySpawned += HandleEnemySpawned;
        }
    }

    private void Update() // 每帧更新
    {
        // 如果玩家在区域内且允许重生，检查是否需要在区域内重生
        if (allowRespawn && IsPlayerInArea() && hasSpawned)
        {
            CheckInAreaRespawn();
        }
    }

    protected override void OnPlayerEnter(GameObject player) // 玩家进入区域
    {
        if (!hasFirstEntered)
        {
            hasFirstEntered = true;
            if (eventTip != null)
            {
                eventTip.ShowEnemyEncountered(enemyCount); // 首次进入显示遭遇敌人提示
            }
        }

        if (spawnOnEnter && enemySpawner != null)
        {
            if (!hasSpawned || (allowRespawn && Time.time - lastSpawnTime >= respawnCooldown)) // 如果未生成过，或允许重生且冷却时间到了
            {
                // 第一次生成时清除已存在的敌人，重生时继续生成
                bool clearExisting = !hasSpawned;
                enemySpawner.SpawnEnemies(enemyCount, eliteSpawnChance, areaDifficulty, baseEnemyLevel, spawnStartDelay, spawnInterval, clearExisting); // 开始生成敌人
                hasSpawned = true;
                lastSpawnTime = Time.time;
            }
            else if (allowRespawn)
            {
                float remainingCooldown = respawnCooldown - (Time.time - lastSpawnTime); // 剩余冷却时间
                Debug.Log($"[{areaName}] 冷却中，还需等待 {remainingCooldown:F1} 秒");
            }
        }
    }

    protected override void OnPlayerExit(GameObject player) // 玩家离开区域
    {
        // 不调用 base.OnPlayerExit(player)，避免显示"离开区域"提示
    }

    private void OnDestroy() // 销毁时清理
    {
        // 取消订阅敌人生成器事件
        if (enemySpawner != null)
        {
            enemySpawner.OnEnemySpawned -= HandleEnemySpawned;

            // 取消订阅所有敌人的死亡事件
            List<Enemy> enemies = enemySpawner.GetSpawnedEnemies();
            foreach (Enemy enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.OnEntityDead -= HandleEnemyDead;
                }
            }
        }
    }

    #endregion

    #region 敌人生成器事件处理

    private void HandleEnemySpawned(Enemy enemy) // 敌人生成回调
    {
        if (enemy != null)
        {
            enemy.OnEntityDead += HandleEnemyDead; // 订阅敌人死亡事件
        }
    }

    private void HandleEnemyDead() // 敌人死亡回调
    {
        OnEnemyKilledEvent?.Invoke(); // 触发敌人死亡事件

        // 检查是否所有敌人都被击杀
        if (enemySpawner != null && enemySpawner.GetAliveEnemyCount() == 0 && enemySpawner.GetSpawnedEnemyCount() > 0)
        {
            OnAreaClearedEvent?.Invoke(); // 触发区域清空事件

            if (eventTip != null)
            {
                eventTip.ShowAreaCleared(enemySpawner.GetSpawnedEnemyCount()); // 显示区域清空提示
            }
        }
    }

    #endregion

    #region 公共API

    public int GetEnemyCount() => enemyCount; // 获取敌人数

    private int GetAliveEnemyCount() // 获取存活敌人数
    {
        return enemySpawner != null ? enemySpawner.GetAliveEnemyCount() : 0;
    }

    public void SetAreaDifficulty(AreaDifficulty difficulty) => areaDifficulty = difficulty; // 设置区域难度

    #endregion
}
