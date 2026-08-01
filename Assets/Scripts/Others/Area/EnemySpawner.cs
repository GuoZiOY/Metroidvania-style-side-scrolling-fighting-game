using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    #region 常量

    private const float RANDOM_SPAWN_RADIUS = 2f; // 随机生成半径
    private const int MIN_LEVEL = 1; // 最小等级

    #endregion

    #region 序列化字段
    [Header("敌人生成")]
    [SerializeField] private List<Enemy> enemyPrefabs; // 敌人预制体列表
    [SerializeField] private Transform[] spawnPoints; // 生成点数组
    #endregion

    #region 私有字段
    private List<Enemy> spawnedEnemies; // 已生成的敌人列表
    #endregion

    #region 事件
    public event Action<Enemy> OnEnemySpawned; // 敌人生成事件
    #endregion



    private void Awake() // 初始化
    {
        spawnedEnemies = new List<Enemy>();
    }

    private void OnDestroy() // 销毁时清理敌人
    {
        ClearSpawnedEnemies();
    }



    #region 公共API

    public int GetSpawnedEnemyCount() => spawnedEnemies.Count; // 获取已生成的敌人数

    public int GetAliveEnemyCount() // 获取存活敌人数
    {
        int count = 0;
        foreach (Enemy enemy in spawnedEnemies)
        {
            if (enemy != null && enemy.gameObject != null && !enemy.IsDead)
            {
                count++;
            }
        }
        return count;
    }

    public List<Enemy> GetSpawnedEnemies() => spawnedEnemies; // 获取已生成的敌人列表

    #endregion

    #region 敌人生成

    public void SpawnEnemies(int enemyCount, float eliteSpawnChance, AreaDifficulty areaDifficulty, int baseEnemyLevel, float startDelay = 0.5f, float interval = 1f, bool clearExisting = false) // 生成敌人
    {
        StartCoroutine(SpawnEnemiesCoroutine(enemyCount, eliteSpawnChance, areaDifficulty, baseEnemyLevel, startDelay, interval, clearExisting));
    }

    private IEnumerator SpawnEnemiesCoroutine(int enemyCount, float eliteSpawnChance, AreaDifficulty areaDifficulty, int baseEnemyLevel, float startDelay, float interval, bool clearExisting) // 敌人生成协程
    {
        yield return new WaitForSeconds(startDelay);

        // 只有在需要时才清除已生成的敌人
        if (clearExisting)
        {
            ClearSpawnedEnemies();
        }

        for (int i = 0; i < enemyCount; i++) // 生成敌人
        {
            bool isElite = ShouldSpawnElite(eliteSpawnChance, areaDifficulty); // 判断是否生成精英
            Enemy enemyPrefab = SelectEnemyPrefab(); // 选择敌人预制体

            if (enemyPrefab == null)
            {
                Debug.LogWarning("没有可用的敌人预制体");
                continue;
            }

            Vector3 spawnPos = GetSpawnPosition(); // 获取生成位置
            Enemy enemy = SpawnEnemy(enemyPrefab, spawnPos); // 生成敌人

            if (enemy != null)
            {
                spawnedEnemies.Add(enemy); // 添加到已生成敌人列表

                // 统一初始化敌人（类型 + 等级）
                EnemyType enemyType = isElite ? EnemyType.Elite : EnemyType.Normal;
                int level = CalculateEnemyLevel(isElite, areaDifficulty, baseEnemyLevel);
                enemy.InitializeEnemy(enemyType, level);

                // V2: 精英敌人注入随机词缀（静态工具，数据库由 GameBootstrap 加载）
                if (isElite)
                    AffixSpawner.ApplyEliteAffixes(enemy, enemyType);

                OnEnemySpawned?.Invoke(enemy); // 触发敌人生成事件
            }

            yield return new WaitForSeconds(interval);
        }

        Debug.Log($"生成了 {spawnedEnemies.Count} 个敌人");
    }

    private bool ShouldSpawnElite(float eliteSpawnChance, AreaDifficulty areaDifficulty) // 判断是否生成精英
    {
        float actualChance = eliteSpawnChance + areaDifficulty.GetEliteChanceBonus();
        return UnityEngine.Random.value < actualChance;
    }

    private Enemy SelectEnemyPrefab() // 选择敌人预制体
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            return null;
        }

        return enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Count)];
    }

    private Vector3 GetSpawnPosition() // 获取生成位置
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].position; // 随机选择一个生成点的位置
        }

        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * RANDOM_SPAWN_RADIUS;
        return transform.position + (Vector3)randomOffset;
    }

    private Enemy SpawnEnemy(Enemy prefab, Vector3 position) // 生成敌人
    {
        if (prefab == null)
        {
            return null;
        }

        Enemy enemy = Instantiate(prefab, position, Quaternion.identity); // 实例化敌人
        return enemy;
    }

    #endregion

    #region 等级计算

    private int CalculateEnemyLevel(bool isElite, AreaDifficulty areaDifficulty, int baseEnemyLevel) // 计算敌人等级
    {
        int level = baseEnemyLevel + areaDifficulty.GetLevelBonus();

        if (isElite)
        {
            level += CalculateEliteLevelBonus();
        }
        else
        {
            level += CalculateNormalLevelFloat(areaDifficulty);
        }

        return Mathf.Max(MIN_LEVEL, level);
    }

    private int CalculateEliteLevelBonus() // 计算精英等级加成
    {
        return UnityEngine.Random.Range(2, 4);
    }

    private int CalculateNormalLevelFloat(AreaDifficulty areaDifficulty) // 计算普通敌人等级浮动
    {
        var (keepChance, lowerChance, upperChance) = areaDifficulty.GetLevelFloatChances();
        float roll = UnityEngine.Random.value;

        if (roll < lowerChance)
        {
            return -1;
        }

        if (roll > 1f - upperChance)
        {
            int maxIncrease = areaDifficulty.GetMaxLevelIncrease();
            return UnityEngine.Random.Range(1, maxIncrease + 1);
        }

        return 0;
    }

    #endregion

    #region 敌人管理

    public void ClearSpawnedEnemies() // 清理已生成的敌人
    {
        foreach (Enemy enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }
        spawnedEnemies.Clear();
    }

    #endregion
}
