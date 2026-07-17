using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneLevelArea : AreaDetectorBase // 场景关卡区域：管理多个子区域和关卡进度
{
    #region 序列化字段

    [Header("关卡信息")]
    [SerializeField] private string levelId; // 关卡ID
    [SerializeField] private int levelIndex; // 关卡序号
    [SerializeField] private AreaDifficulty levelDifficulty = AreaDifficulty.Normal; // 关卡难度

    [Header("子区域管理")]
    [SerializeField] private List<EnemyArea> enemyAreas; // 敌人区域列表
    [SerializeField] private bool autoActivateSubAreas = true; // 是否自动激活子区域

    #endregion

    #region 私有字段

    private int totalEnemies; // 总敌人数（关卡目标）
    private int remainingEnemies; // 剩余敌人数
    private bool isLevelComplete; // 关卡是否完成
    private bool hasEnteredLevel; // 是否已进入关卡
    private Player player; // 玩家引用

    #endregion

    #region 事件

    public event Action<int, int> OnEnemyKilled; // 敌人被击杀事件（剩余数量，总数）
    public event Action OnLevelComplete; // 关卡完成事件
    public event Action OnLevelFailed; // 关卡失败事件

    #endregion

    #region 生命周期

    protected override void Awake() // 初始化
    {
        base.Awake();
        InitializeSubAreas();
        FindPlayer();
    }

    private void FindPlayer() // 查找玩家
    {
        player = FindObjectOfType<Player>();
        if (player != null)
        {
            player.OnEntityDead += HandlePlayerDeath; // 订阅玩家死亡事件
        }
    }

    protected override void OnPlayerEnter(GameObject player) // 玩家进入关卡
    {
        if (!hasEnteredLevel)
        {
            hasEnteredLevel = true;
            base.OnPlayerEnter(player); // 只在首次进入时显示进入提示
            OnFirstEnter();
        }

        if (autoActivateSubAreas)
        {
            ActivateSubAreas();
        }
    }

    protected override void OnPlayerExit(GameObject player) // 玩家离开关卡
    {
        if (autoActivateSubAreas)
        {
            DeactivateSubAreas();
        }
    }

    private void OnDestroy() // 销毁时清理，切换场景的时候用到
    {
        CleanupSubAreas();
        if (player != null)
        {
            player.OnEntityDead -= HandlePlayerDeath; // 取消订阅玩家死亡事件
        }
    }

    #endregion

    #region 关卡失败处理

    private void HandlePlayerDeath() // 玩家死亡回调
    {
        if (!isLevelComplete && hasEnteredLevel)
        {
            StartCoroutine(FailLevelCoroutine());
        }
    }

    private IEnumerator FailLevelCoroutine() // 关卡失败协程
    {
        yield return new WaitForSeconds(1f); // 延迟1秒
        OnLevelFailed?.Invoke();

        if (eventTip != null)
        {
            eventTip.ShowLevelFailed(areaName); // 显示关卡失败提示
        }
    }

    #endregion

    #region 子区域管理

    private void InitializeSubAreas() // 初始化子区域
    {
        if (enemyAreas == null)
        {
            enemyAreas = new List<EnemyArea>();
        }

        foreach (EnemyArea area in enemyAreas)
        {
            if (area != null)
            {
                area.SetAreaDifficulty(levelDifficulty);
                area.OnEnemyKilledEvent += HandleEnemyKilled; // 订阅敌人死亡事件
            }
        }

        CalculateTotalEnemies(); // 初始化时计算总敌人数量
    }

    private void ActivateSubAreas() // 激活子区域
    {
        foreach (EnemyArea area in enemyAreas)
        {
            if (area != null)
            {
                area.gameObject.SetActive(true);
            }
        }
    }

    private void DeactivateSubAreas() // 停用子区域
    {
        foreach (EnemyArea area in enemyAreas)
        {
            if (area != null)
            {
                area.gameObject.SetActive(false);
            }
        }
    }

    private void CleanupSubAreas() // 清理子区域，防止内存泄漏
    {
        if (enemyAreas == null) return;

        foreach (EnemyArea area in enemyAreas)
        {
            if (area != null)
            {
                area.OnEnemyKilledEvent -= HandleEnemyKilled; // 取消订阅敌人死亡事件
            }
        }
    }

    #endregion

    #region 关卡进度

    private void CalculateTotalEnemies() // 计算总敌人数（关卡目标）
    {
        totalEnemies = 0;
        foreach (EnemyArea area in enemyAreas)
        {
            if (area != null)
            {
                totalEnemies += area.GetEnemyCount();
            }
        }
        remainingEnemies = totalEnemies; // 初始化剩余敌人数量
    }

    private void HandleEnemyKilled() // 敌人死亡回调
    {
        if (isLevelComplete) return;

        remainingEnemies--; // 剩余数量-1
        remainingEnemies = Mathf.Max(0, remainingEnemies); // 确保不为负数

        OnEnemyKilled?.Invoke(remainingEnemies, totalEnemies); // 触发敌人死亡事件

        CheckLevelComplete(); // 检查是否完成关卡
    }

    private void CheckLevelComplete() // 检查关卡是否完成
    {
        if (isLevelComplete) return;

        if (remainingEnemies == 0 && totalEnemies > 0) // 剩余数量为0时完成
        {
            StartCoroutine(CompleteLevelCoroutine());
        }
    }

    private IEnumerator CompleteLevelCoroutine() // 关卡完成协程
    {
        yield return new WaitForSeconds(1f); // 延迟1秒

        isLevelComplete = true;
        OnLevelComplete?.Invoke();

        if (eventTip != null)
        {
            eventTip.ShowLevelComplete(areaName, GetDefeatedEnemies()); // 显示关卡完成提示
        }
    }

    #endregion

    private void OnFirstEnter() // 首次进入关卡
    {
        CalculateTotalEnemies(); // 计算敌人数量

        if (eventTip != null)
        {
            eventTip.ShowLevelEnter(areaName); // 显示进入关卡提示
        }
    }


    #region 公共API

    public string GetLevelId() => levelId; // 获取关卡ID
    public int GetLevelIndex() => levelIndex; // 获取关卡序号
    public AreaDifficulty GetLevelDifficulty() => levelDifficulty; // 获取关卡难度
    public List<EnemyArea> GetEnemyAreas() => enemyAreas; // 获取敌人区域列表

    public int GetTotalEnemies() => totalEnemies; // 获取总敌人数（关卡目标）
    public int GetRemainingEnemies() => remainingEnemies; // 获取剩余敌人数
    public int GetDefeatedEnemies() => totalEnemies - remainingEnemies; // 获取已击败敌人数
    public float GetProgress() => totalEnemies > 0 ? (float)(totalEnemies - remainingEnemies) / totalEnemies : 0f; // 获取进度（0~1）

    public bool IsLevelComplete() => isLevelComplete; // 关卡是否完成
    public bool HasEnteredLevel() => hasEnteredLevel; // 是否已进入关卡

    public void SetLevelDifficulty(AreaDifficulty difficulty) // 设置关卡难度
    {
        levelDifficulty = difficulty;
        foreach (EnemyArea area in enemyAreas)
        {
            if (area != null)
            {
                area.SetAreaDifficulty(difficulty);
            }
        }
    }



    #endregion
}
