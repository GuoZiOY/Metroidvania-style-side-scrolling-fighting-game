## Technology Stack
- **Engine**: Unity 6000.4.8f1
- **Language**: C#
- **Rendering**: URP
- **IDE**: JetBrains Rider
- **Build System**: Unity Build Pipeline
- **Asset Pipeline**: Unity Asset Import Pipeline + Addressables

## Engine Version Reference

@docs/engine-reference/unity/VERSION.md

# 项目代码规范

## 注释风格

- 不使用 `/// <summary>` XML 注释
- 使用 `//` 行内注释说明代码意图
- **所有字段（含 `[SerializeField]`）、方法、方法内关键逻辑段落必须有行内注释**

```csharp
// ✅ 正确 — 字段注释说明用途
[SerializeField] private float auraRadius = 3f;     // 光环半径（米）
[SerializeField] private float damagePerTick = 5f;  // 每跳火伤
[SerializeField] private float tickInterval = 0.5f; // 间隔（秒）

private float lastTickTime;  // 上次判定时间
private Transform player;    // 缓存的玩家引用

// ✅ 正确 — 方法注释说明职责
// 火焰光环词缀 — 周期性对光环范围内的玩家造成火元素伤害
public class FireAuraAffix : MonoBehaviour, IEnemyAffix
{
    // 生成时调用：初始化缓存、订阅事件
    public void OnApplied(Enemy enemy)
    {
        this.enemy = enemy;
        enemy.OnEnemyDealtDamage += OnDealtDamage; // 订阅伤害事件，每次命中累加充能
    }

    // 每帧 Battle 状态调用
    public void OnBattleUpdate(Enemy enemy)
    {
        // 冷却检查
        if (Time.time - lastTickTime < tickInterval)
            return;

        // 缓存玩家引用，避免每帧 GetComponent
        if (player == null)
            player = enemy.GetPlayerReference();

        // 距离判定：进入光环范围则造成火伤
        float dist = Vector2.Distance(enemy.transform.position, player.position);
        if (dist <= auraRadius)
        {
            var health = player.GetComponentInParent<Entity_Health>();
            if (health != null)
                health.TakeDamage(0, damagePerTick, ElementType.Fire, enemy.transform);
        }
    }
}

// ❌ 错误 — 无注释或注释不足
[SerializeField] private float auraRadius = 3f;
private float lastTickTime;

public void OnApplied(Enemy enemy)
{
    this.enemy = enemy;
    enemy.OnEnemyDealtDamage += OnDealtDamage;
}

public void OnBattleUpdate(Enemy enemy)
{
    if (Time.time - lastTickTime < tickInterval)
        return;
    // ... 10 行无注释逻辑
}
```

## 命名规范

- 私有字段不使用 `_` 前缀
- 遵循 Unity 常规命名惯例（PascalCase 公开成员、camelCase 私有）

## 语句风格

- `if` / `for` / `foreach` / `while` 语句的条件后必须换行，**即使是单行语句也必须换行**：

```csharp
// ✅ 正确 — 单行语句也换行
if (condition)
    DoSomething();

if (!isActive)
    return;

for (int i = 0; i < count; i++)
    Process(i);

// ✅ 正确 — 多行用花括号
if (condition)
{
    DoSomething();
    DoSomethingElse();
}

// ❌ 错误 — 条件后不换行
if (condition) DoSomething();
if (!isActive) return;
for (int i = 0; i < count; i++) Process(i);
```
