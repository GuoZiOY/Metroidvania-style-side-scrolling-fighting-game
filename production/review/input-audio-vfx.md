# 输入/音频/VFX系统系统审查报告

> 审查日期：2026-08-18 | 代码基线：git HEAD（7c7b82c6dff06f0bd5032fcaf4a40ea5fea9c466）
>
> 审查范围：`Assets/Scripts/InputSystem`、`Assets/Scripts/AudioSystem`、`Assets/Scripts/VFX`、`Assets/Scripts/Parallax`（含强关联的 `Character/Entity/Entity_VFX.cs`、`Character/Player/Player_VFX.cs`、`Character/CombatSystem/HitStopManager.cs`、`Character/Enemy/Boss/BossEncounter.cs` 等上下游消费方）
>
> 参考文档：`design/gdd/input-system.md`、`design/gdd/audio-system.md`、`docs/architecture/architecture.md`（F1 输入 / C13 音频 模块所有权）

---

## 1. 框架结构

### 1.1 输入系统（F1）— 静态门面 + 组件

| 类 | 模式 | 职责 |
|---|---|---|
| `GameInput`（静态类） | 静态门面 | Action 枚举（21 个动作）、`Dictionary<Action, KeyCode>` 键位表、PlayerPrefs 持久化（`GameInputBindings`）、`GetKeyDown/GetKey/GetKeyUp/Horizontal/Vertical` 统一入口、`IsGameBlocked` 阻塞策略、`OnBindingsChanged` 静态事件 |
| `UI_KeyRebind`（MonoBehaviour） | UI 组件 | 单键重绑界面：等待按键 → `SetBinding` → 刷新标签 |

- **设计要点**：纯 `Input.GetKey` 数字轴（无 Input Manager / Input System 包依赖），架构文档 F1「V2 变更：无」基本成立。
- **职责单一性**：`GameInput` 一人承担「动作枚举、键位映射、阻塞策略、持久化」四件事，尚可接受；但阻塞策略硬编码了对 `Networking.UI_Chat` 与 `UIManager` 的引用（见 §3 信息链路 的耦合问题）。
- **无单例**：静态类天然全局，配合 `PlayerPrefs` 自持持久化，未接入 SaveManager（合理，见 §3）。

### 1.2 音频系统（C13）— 单例 + 分组序列化配置

| 类 | 模式 | 职责 |
|---|---|---|
| `AudioManager`（MonoBehaviour，351 行） | 单例 + DontDestroyOnLoad | 8 个 `AudioSource` 常驻池（BGM×2、环境、UI、命中、额外、暴击、打字机）；7 个 `[System.Serializable]` 音频分组（BGM/环境/UI音效/角色/战斗/存档/打字机）；音量三通道（master/BGM/SFX）存 PlayerPrefs；BGM 双源 crossfade + 栈式 push/pop；按钮音效全局挂接 |
| `UI_ButtonSfx`（MonoBehaviour） | 挂载组件 | `Start` 时把按钮 onClick 注册到 AudioManager（与全局挂接双轨并存，见 §6-M3） |

- **优点**：BGM 栈 + 双源交叉淡入 + `unscaledDeltaTime` 渐变 + 协程竞态防护（`StopCoroutine` 旧协程）实现扎实；`BossEncounter` 只触发开/关，曲目集中配置，符合架构文档 C13「V2 变更：Boss BGM 切换」。
- **问题**：脚步声/跳跃/落地共用 `uiSource`（语义错位）；若干显式 `PlayButtonSfx` 调用与全局自动挂接叠加，存在双重发声风险（§6-M3）。

### 1.3 VFX 系统 — 组件式、无统一管理器

| 类 | 模式 | 职责 |
|---|---|---|
| `CounterShockwaveEffect` | 组件 | 反击冲击波：收缩/扩张粒子 + 环形波 + 星光，支持顿帧 Pause/Resume |
| `HitSlashEffect` | 组件 | 斩击溶解特效：缩放 + `_DissolveAmount` 材质溶解 + 纹理滑动，自销毁 |
| `CritParticleEffect` | 组件 | 暴击双爆粒子（core/outer burst） |
| `CinemaScreenShake` | 组件 | Cinemachine ImpulseSource 封装：暴击/反击/三连击/跳攻/通用 `ShakeWith` |
| `Entity_VFX`（Character/Entity） | 基类组件 | 受击变色、伤害飘字、元素色、命中 VFX、震屏转发 |
| `Player_VFX : Entity_VFX` | 派生组件 | 残影、反击冲击波、落地特效、跳跃攻击落地 |

- **特征**：无 VFX 管理器/对象池，全部「Instantiate → 播放 → Destroy」，`GetComponent<ParticleSystem>` 逐次查找（一次性的，可接受）。没有统一的事件总线，靠 `player.VFX.xxx()` 直接调用。
- **碎片化**：VFX 触发逻辑散落在 `Player_Combat`、`Entity_VFX`、`Player_VFX`、玩家各状态类，无统一「特效请求」入口；元素着色意图存在但被 `CritParticleEffect` 丢弃参数破坏（§6-M4）。

### 1.4 Parallax — 场景配置组件

| 类 | 模式 | 职责 |
|---|---|---|
| `ParallaxBackground`（MonoBehaviour） | 驱动者 | Awake 计算相机半宽、FixedUpdate 按相机位移驱动各层 |
| `ParallaxLayer`（`[System.Serializable]` 纯类） | 数据+逻辑 | 每层 multiplier、宽度计算、无缝循环（LoopBackground） |

- 结构简单清晰；但文件名为 `Parallax _Background.cs`（含空格、与类名 `ParallaxBackground` 不符），且两文件为 GBK 编码（§5/§6-M7）。

---

## 2. 工作流程

### 2.1 每帧输入 → 战斗 → 音频/VFX 反馈链路

1. `Player.Input()` 调 `GameInput.Horizontal/Vertical` 与各 `GetKeyDown`（跳跃/攻击/冲刺/反击），写入 `Player_InputBuffer`（输入缓冲）
2. 状态机各状态（`Player_GroundedState` 等）读缓冲/按键完成状态转换
3. `Player_Combat.PerformAttack()`：空挥 → `PlaySwingSfx(index)`；命中 → `base.PerformAttack()` 伤害管道（7 步，C1）
4. `OnTargetHit`：`PlayHitSfx(index)` + `PlayExtraSfx(index)`，暴击追加 `PlayCritSfx(index)`
5. 暴击/连击尾击 → `HitStopManager.TriggerLocalHitStop`（三段式：冻结→缓恢复→快恢复，动画速度控制）
6. 命中 VFX：`Entity_VFX.CreateOnHitVFX` → 暴击走 `CritParticleEffect.CreateCritParticles` + `ShakeScreen`；非暴击 Instantiate hitVFX
7. 玩家被局部顿帧时 `Player.StartHitStop → ToggleShockwavePause(true)`，结束恢复

### 2.2 反击演出链路（输入 → 音频 → VFX → 顿帧）

1. `Player_Combat.CounterAttackPerformed()` 检测 `ICounterable.IsInCounterTime`
2. `PlayCounterSuccessSfx()` + `PlayCounterHitSfx()`（内部 `DelayedCounterHit` 协程延迟 `counterHitDelay=0.1s`）
3. `player.VFX.ShakeScreenForCounter()`（朝向修正的 impulse）+ `DoCounterVisuals` → `CounterShockwaveEffect.Spawn`（收缩/扩张随机 + 环形波 + 不参与顿帧的星光）
4. `counterable.HandleCounter()`（立即眩晕/击退，防止顿帧期间二次反击——有注释说明）
5. `DelayedHitStopEffect` 延 1 帧（等粒子生成）→ 局部顿帧 → `ToggleShockwavePause` 冻结冲击波粒子

### 2.3 Boss 战 BGM 切换链路

1. `BossEncounter.StartFight()` → `PushBossBgm(0.5f)` → 内部 `PushBgm(bossBgmClip)`
2. `PushBgm`：`GetCurrentBgmClip()` 压栈上一曲 → 停旧协程 → `CrossfadeTo` 协程（双源 ping-pong，`unscaledDeltaTime` 渐变）
3. Boss 死亡 → `PopBgm(0.8f)` 弹栈恢复；`OnDestroy → PopBgm(0f)` 防 BGM 跨场景残留

### 2.4 音量设置链路

1. `UI_Setting` 滑条 → `SetMasterVolume/SetBgmVolume/SetSfxVolume(int)`（0–10 整数）
2. 写 PlayerPrefs（`Audio_MasterVolume/BgmVolume/SfxVolume`），BGM 即时 `UpdateBgmVolume`
3. 播放时 `Vol(group) = sfxVolume * masterVolume * groupVolume` 统一乘算（无 AudioMixer）

### 2.5 键位重绑链路

1. `UI_KeyRebind.StartRebind` → 置 `isRebinding`，标签显示「按下按键...」
2. `OnGUI()`（IMGUI）捕获 `Event.current.keyCode` → `GameInput.SetBinding`
3. `SetBinding` → 写字典 → `SaveBindings()`（PlayerPrefs）→ `OnBindingsChanged` 事件 → `UI_SkillSlot.UpdateKeyText` 等刷新

### 2.6 输入阻塞链路

1. `IsGameBlocked = Networking.UI_Chat.IsChatFocused || UIManager.IsAnyPanelOpen || IsPlayerControlBlocked`
2. 所有非 Toggle 动作的 `GetKeyDown/GetKey/GetKeyUp` 与轴先查阻塞 → 命中返回 false/false
3. 例外（IsToggleAction）：Escape/Interact/四个面板切换键不受阻塞（面板打开也能关）
4. **缺陷**：`SkillSlotManager.HandleSkillSlotInput` 每帧裸 `Input.GetKeyDown` 完全绕过此链路（§6-S2）

### 2.7 视差滚动链路

1. `Awake`：`Camera.main` + `orthographicSize * aspect` 半宽 + 各层 `CalculateImageWidth`
2. `FixedUpdate`：相机 X 位移差 → 各层 `Move(位移 × multiplier)` → `LoopBackground(相机左右边缘)` 无缝循环（右移一图宽或左移一图宽）

---

## 3. 信息链路

### 3.1 事件/数据流盘点

| 链路 | 类型 | 说明 |
|---|---|---|
| `GameInput.OnBindingsChanged` | C# event（静态） | UI_SkillSlot 订阅刷新技能槽键位文本 |
| `AudioManager` 按钮注册 | `Button.onClick` 监听 | 全局挂接（sceneLoaded 全扫描）+ `UI_ButtonSfx` 组件双轨 |
| 音频触发 | 直接方法调用 | `AudioManager.Instance?.PlayXxx()`，全项目 44 处调用点 |
| VFX 触发 | 直接方法调用 | `player.VFX.ShakeScreenForAttack(...)`、`DoCounterVisuals(...)` |
| 顿帧 ↔ 粒子 | 覆写回调 | `Player.StartHitStop/EndHitStop → ToggleShockwavePause` |
| 输入消费 | 静态直读 | Player FSM、UIManager、NPCBehaviour、Checkpoint、Portal、VerticalPlatform、SkillSlotManager、UI_SkillSlot 等 |

### 3.2 与上下游依赖

- **上游依赖**：GameInput 被 Player 状态机、UIManager、交互系统（NPC/检查点/传送门/竖平台）直接消费——输入层是全局门面，符合 F1 定位。
- **AudioManager 消费方**：Player_Combat（挥砍/命中/暴击/反击）、玩家各状态（跳跃/落地/脚步）、BossEncounter（BGM）、SaveManager/Checkpoint（存档读档音）、Gold/Object_Chest（拾取/宝箱）、TypewriterEffect（打字机）、全部 UI 面板（按钮/拒绝音）。
- **VFX 消费方**：Entity_VFX（暴击粒子/震屏）→ Player_VFX（反击/落地/残影）→ 玩家状态类；`CinemaScreenShake.ShakeWith` 供 Boss 落地等任意来源使用。
- **Parallax**：纯场景自洽，无外部依赖（`Camera.main` 除外）。

### 3.3 SaveManager 覆盖情况（读档链路）

- `SaveManager.CollectSaveData()`（SaveManager.cs L237-277）收集：Player/属性/背包/仓库/装备/技能/任务/WorldState —— **不包含**输入键位、音量、VFX 状态。
- **结论：读档链路完整，且正确**。三系统数据均为「设备级/瞬时」：
  - 键位与音量 → `PlayerPrefs` 自持、变更即时 `Save()`，不随存档槽走（符合「设置设备级、进度存档级」的常规分层）；
  - VFX/Parallax → 全部瞬时生成或场景配置，无存档语义。
- **风险提示**：① 键位/音量跨存档槽共享，若未来做「每档案设置」需迁移；② PlayerPrefs 无版本号/迁移机制（`SAVE_DATA_VERSION` 只覆盖存档文件），键格式变更即丢失旧设置；③ 上述「设备级」决策未在任何文档中声明，属隐性设计。

### 3.4 耦合问题（架构倒置）

- `GameInput`（FOUNDATION 层）直接引用 `Networking.UI_Chat` 与 `UIManager`（上层 UI/网络模块）——底层依赖上层，且架构文档 F1 声明只消费 `ModalStack`。若剥离网络模块或 UI 重构，输入层直接编译失败。
- `UI_ButtonSfx` 与 AudioManager 全局挂接双轨并存（同一职责两套机制）。
- `CinemaScreenShake` 缓存 `Player` 引用（`Start` 时 `FindAnyObjectByType`）——当前 Player 是 `DontDestroyOnLoad` 持久对象，引用不失效，但该假设未文档化，一旦改为场景内生成即 NRE。

---

## 4. 与设计文档一致性

### 4.1 `design/gdd/input-system.md`（反向文档，2026-07-28）

| 文档声明 | 实现现状 | 差异 |
|---|---|---|
| Action 枚举 33 个动作 | 实际 **21 个**（移动4+战斗5+技能6+面板4+通用2） | ❌ 过时 |
| `IsGameBlocked = IsChatFocused \|\| ModalStack.IsAnyModalOpen` | 实际为 `IsChatFocused \|\| UIManager.IsAnyPanelOpen \|\| IsPlayerControlBlocked` | ❌ 少 `IsPlayerControlBlocked`（1e1a010 新增未回写文档） |
| 键位持久化格式、SetBinding/ResetToDefaults 流程 | 一致 | ✅ |
| Horizontal/Vertical 轴 | 一致 | ✅ |

### 4.2 `design/gdd/audio-system.md`（反向文档，2026-07-28）

| 文档声明 | 实现现状 | 差异 |
|---|---|---|
| 「数组音效随机选择: `clips[Random.Range(...)]`」 | 仅 `PlayCounterSuccessSfx` 用 `RandomClip`；跳跃/落地按 `isDoubleJump` **确定性**取 index 0/1；挥砍/命中/暴击按连击 index `SafeClip` 确定性取 | ❌ 文档与实现不符 |
| API 列表 | 缺 `PushBgm/PopBgm/PushBossBgm/GetCurrentBgmClip/RegisterButton/PlayFootstepSfx/PlayChestSfx/PlayGoldPickupSfx/PlayJumpAttackExtraSfx/PlayTypewriterSfx/StopTypewriterSfx/音量组` | ❌ 大量新增 API 未回写（1f0872f、b76b8e1 等提交） |
| UI_ButtonSfx 组件职责 | 组件仍存在，但 AudioManager 已全局自动挂接，组件近乎冗余 | ⚠️ 职责重叠未文档化 |

### 4.3 `docs/architecture/architecture.md`

| 声明 | 现状 | 差异 |
|---|---|---|
| F1 Owns/Exposes | GameInput 静态类、轴、IsGameBlocked | ✅ 一致 |
| F1 Consumes: `ModalStack` | 实际 `Networking.UI_Chat + UIManager + IsPlayerControlBlocked` | ❌ 过时（且是层依赖倒置） |
| F1「V2 变更：无」 | 已有 `IsPlayerControlBlocked`（未置位）与 UI_KeyRebind | ⚠️ 部分过时 |
| C13 音频分组/API | 分组与「Boss BGM 切换」V2 变更已落地 | ✅ 一致 |
| Save/Load Path | 与 §3.3 一致（存档不含输入/音频） | ✅ 一致 |

### 4.4 总体结论

三份文档均为「反向文档」属性，说明实现先行、文档滞后；近 6 个提交（crossfade、BGM 栈、玩家控制锁、通用震屏）均未回写文档。**文档漂移是本系统的常态问题**，建议在提交链上加「改系统代码必同步反向文档」的纪律或定期重跑反向文档流程。

---

## 5. 代码质量

### 5.1 注释规范（对照 CLAUDE.md：字段/方法/关键逻辑须行内中文注释）

- ✅ 达标：`GameInput`（阻塞/数字轴/加载解析均有注释）、`AudioManager`（BGM 栈、crossfade 竞态、延迟命中均有解释性注释，质量高）、`CounterShockwaveEffect`、`ParallaxBackground`
- ⚠️ 部分达标：`CinemaScreenShake`（`screenShake` 字段无注释、Awake 提前获取有注释）、`HitSlashEffect`（部分魔法数字无说明）、`CritParticleEffect`（仅 Header）
- ❌ 不达标：`UI_KeyRebind` —— **全文件 50 行无任何一处注释**（字段、方法、OnGUI 逻辑全裸），直接违反项目规范
- ❌ 编码损坏：`ParallaxLayer.cs` L30 注释已乱码（`//??????`）

### 5.2 命名与文件组织

- ❌ `Parallax _Background.cs`：文件名含空格 + 与类名 `ParallaxBackground` 不一致（Unity 资源/源码命名违规）
- ⚠️ AudioManager 中文字段标识符（`环境`/`UI音效`/`角色`/`战斗`/`存档`/`打字机`）与英文混用，风格不统一（合法但不推荐）
- ⚠️ `GameInput.s_bindings` 使用 `s_` 前缀，与项目「无前缀」惯例不一致（虽不违反「不用 `_` 前缀」字面规则）
- ⚠️ 拼写错误：`Entity_VFX.ChockVFX`（应为 Shock）、`ParallaxLayer.LoopBackground(cameraLefteEdge)` 参数拼写

### 5.3 语句风格（对照 CLAUDE.md：if/for 条件后必须换行）

**系统性违规**——以下均违反「单行 if 也必须换行」规范：
- `GameInput.cs` L70/75/80：`if (IsGameBlocked && !IsToggleAction(action)) return false;`
- `AudioManager.cs` L120、L207、L230、L282 等 10+ 处单行 if
- `HitSlashEffect.cs` L16-17、`CinemaScreenShake.cs` L52/60/71 等、`CounterShockwaveEffect.cs` L27/31、`ParallaxLayer.cs` L36-37、`UI_KeyRebind.cs` L22/28
- 结论：该规范在四个系统内均未被执行，属全局性风格债，建议批量格式化（Rider 格式化规则落库）而非逐文件修。

### 5.4 性能风险

| 位置 | 风险 | 等级 |
|---|---|---|
| `AudioManager.HookSceneButtons` → `Resources.FindObjectsOfTypeAll<Button>()` | 每次场景加载全资源域扫描（含已加载资源），一次性但昂贵 | 中 |
| `BossEncounter.StartFight` while 循环每帧 `FindAnyObjectByType<Boss_SlimeKing>()` | 整场 Boss 战每帧场景级搜索（GC + 遍历） | 中 |
| `HitStopManager.UpdateLocalHitStop` 每帧 `new List<>()` | 顿帧期间每帧分配（GC） | 低（范围外，顺带提及） |
| `GameInput.Horizontal` 每取一次做 2 次字典查找 + 2 次 `Input.GetKey` | 每帧多次调用，可忽略 | 低 |
| `SkillSlotManager.Update` 每帧 5 次 `Input.GetKeyDown` | 轮询式，可忽略；问题在绕过阻塞（§6-S2） | 低 |
| VFX 全 Instantiate/Destroy | 无对象池，频繁触发场景 GC；MVP 可接受，V2 建议池化 | 低 |
| `HitSlashEffect` 每帧 `mat.SetFloat/SetVector` | 单对象逐帧写材质属性，量小可接受 | 低 |

### 5.5 魔法数字 / 硬编码

- 已序列化可调（OK）：crossfade 时长、顿帧时长、震屏力度、`sortingOrder=20`、`imageWidthOffset=10`
- 未序列化硬编码：`HitSlashEffect` 淡出 0.1f、纹理滑动 0.2f、销毁缓冲 0.5f（`CounterShockwaveEffect`/`CritParticleEffect`）
- 结构性硬编码：`PlayJumpSfx/PlayLandingSfx` 的 `Mathf.Min(1, len-1)` 隐含「数组恰好 2 个元素」假设
- 常量管理良好：PlayerPrefs 键均为 const（`GameInputBindings`、`Audio_MasterVolume` 等）

### 5.6 超大类检查

四个系统内无 >400 行文件（最大 `AudioManager` 351 行，接近阈值但结构尚清晰）。`Entity_VFX.cs`（范围外，191 行）存在乱码注释与未使用 `using`（`System.Diagnostics`/`System.Xml.Linq`），建议顺手清理。

### 5.7 其他

- `IsPlayerControlBlocked`（GameInput L64）：声明且被 `IsGameBlocked` 引用，但全项目**无任何置位点**——死代码 + 误导注释（「BossEncounter 置位」，实际 BossEncounter 只锁传送门）
- `CritParticleEffect.CreateCritParticles(Vector3, Color _ = default)`：参数被完全忽略（§6-M4）

---

## 6. 问题与改进建议（按严重度分级）

### 🔴 严重（S）— 功能性 Bug，直接影响玩家操作

**S1. 默认键位冲突：SkillSlot5 与 DomainExpansion 都绑定 `KeyCode.O`**
- 位置：`Assets/Scripts/InputSystem/GameInput.cs` L131-132
- 现象：按下 O 键同时触发「技能槽 5」与「领域展开」（`PlayerState.cs` L36 查 DomainExpansion，`SkillSlotManager` 裸查 O 键触发槽 5），技能资源被双份消耗，且 UI_SkillSlot 两处键位显示均为 O。
- 建议：`DomainExpansion` 改用独立键（如 `KeyCode.R`）；`SetBinding` 增加「同键冲突检测」（写前扫描已有绑定，冲突则拒绝并提示，或自动解绑旧动作），从源头杜绝。

**S2. 技能槽输入绕过 `GameInput.IsGameBlocked` 阻塞策略**
- 位置：`Assets/Scripts/SkillSyetem/SkillSlotManager.cs` L93-102（`HandleSkillSlotInput` 每帧裸 `Input.GetKeyDown(GetSlotKey(i))`）
- 现象：① 任意 UI 面板打开（商店/铁匠/角色/背包…）期间技能仍可释放——面板设计意图是「打开即暂停」（`Time.timeScale=0`）但输入未被禁；② 聊天输入框聚焦打字时，按下 H/Y/U/I/O 会**误触发技能**（H/Y/U/I/O 恰为默认技能键 1-5），且 Y 键同时是 `UI_Chat.toggleKey`（`UI_Chat.cs` L21、L105），打字输入 y 会直接关闭聊天——聊天与技能输入完全互踩；③ `IsPlayerControlBlocked` 若未来启用，技能同样不受控。
- 建议：`HandleSkillSlotInput` 改为 `GameInput.GetKeyDown(GameInput.Action.SkillSlot1..5)`，复用阻塞与重绑链路（顺带消除与 `GetSlotKey` 重复的映射逻辑，直接删 `GetSlotKey`/`Input` 依赖）；UI_Chat 的 toggleKey 改为走 `GameInput` 或移除 Y 键默认值并加入重绑系统。

### 🟡 中等（M）— 架构/一致性问题，可能引发线上缺陷

**M1. 三份设计/架构文档与实现漂移**
- 位置：`design/gdd/input-system.md`（33 动作、缺 IsPlayerControlBlocked）、`design/gdd/audio-system.md`（随机选曲描述错误、API 缺 push/pop/打字机等）、`docs/architecture/architecture.md`（F1 Consumes 写 ModalStack）
- 建议：本次审查后一次性回写三份文档；后续提交链强制「改代码必同步反向文档」，或将反向文档纳入 CI 定期生成。

**M2. 输入层（FOUNDATION）依赖上层 UI/网络模块（层依赖倒置）**
- 位置：`Assets/Scripts/InputSystem/GameInput.cs` L66
- 现象：`Networking.UI_Chat.IsChatFocused`、`UIManager.IsAnyPanelOpen` 被底层静态类硬引用；架构文档声明 F1 只消费 `ModalStack`。剥离网络/重构 UI 时输入层编译失败。
- 建议：抽象 `IInputBlockSource`（`bool IsBlocked { get; }`）由 UI/网络模块注册到 GameInput（组合式阻塞），或至少把两个引用收敛到一个「阻塞聚合器」静态类，与架构文档对齐。

**M3. 按钮音效双轨注册 → 双重发声风险 + 全场景扫描**
- 位置：`Assets/Scripts/AudioSystem/AudioManager.cs` L269-285（`HookSceneButtons`/`RegisterButton`）
- 现象：AudioManager 在每次 sceneLoaded 全局把 `PlayButtonSfx` 挂到**所有** Button 上；而 `UI_ShopPanel`、`UI_WarehousePanel`、`UI_TreeNode`、`UI_BaseSlot`、`UI_ItemSlot` 等又在各自点击处理器里显式调 `PlayButtonSfx()`——静态面板按钮点击会**双重发声**；`Resources.FindObjectsOfTypeAll<Button>()` 每次场景加载全资源域扫描也有性能开销。
- 建议：二选一——保留全局挂接则删除所有显式调用；保留显式调用则去掉全局挂接、改为 `UI_ButtonSfx` 组件单轨注册。推荐后者（显式、可控），并把 HookSceneButtons 改为按需（如仅挂接主菜单/设置页）。

**M4. 暴击粒子元素着色参数被忽略**
- 位置：`Assets/Scripts/VFX/CritParticleEffect.cs` L9-13；调用方 `Assets/Scripts/Character/Entity/Entity_VFX.cs` L129-151
- 现象：`CreateCritParticles(position, Color _ = default)` 的 `Color _` 参数从未使用；调用方 `UpdateOnHitColor(element)` 精心计算的冰蓝/火红/雷黄粒子颜色被丢弃——元素色视觉设计完全失效（且 `Entity_VFX` 还 `Debug.Log("CreateCritParticles")` 每暴击刷日志）。
- 建议：实现颜色应用（如遍历 `ParticleSystem` 设 `startColor`，或对 burst 材质 `_BaseColor` 着色）；删除失效参数与调试日志。

**M5. Boss 战每帧 `FindAnyObjectByType<Boss_SlimeKing>()`**
- 位置：`Assets/Scripts/Character/Enemy/Boss/BossEncounter.cs` L71-79
- 现象：整场 Boss 战 while 循环每帧做场景级搜索判断死亡，持续 GC 与遍历开销。
- 建议：改为在 `boss.EntityDead`/Boss 死亡事件上订阅（项目已有 `Enemy.EntityDead` 事件模式），或持有 boss 引用判 `boss == null || !boss.gameObject.activeSelf`。

**M6. `IsPlayerControlBlocked` 死代码 + 误导注释**
- 位置：`Assets/Scripts/InputSystem/GameInput.cs` L64
- 现象：字段被 `IsGameBlocked` 引用但全项目无置位点；注释称「BossEncounter 置位」而 BossEncounter 实际未置位（只锁传送门）。若 Boss 出场演出未来需要锁玩家操作，该钩子是好的预留；但现在既无实现也无调用，属未落地功能。
- 建议：要么由 BossEncounter（或新的 Boss 演出逻辑）真正置位/复位并验证，要么删除该字段避免误导；顺带在 GDD 中记录该预留意图。

**M7. Parallax 编码与命名违规**
- 位置：`Assets/Scripts/Parallax/Parallax _Background.cs`、`ParallaxLayer.cs`
- 现象：两文件为 GBK 编码（非 UTF-8，静态分析/read 工具无法直接解析、git diff 产生噪音）；文件名含空格且与类名不一致；`ParallaxLayer.cs` L30 注释已乱码。
- 建议：转换为 UTF-8（带 BOM 亦可），重命名为 `ParallaxBackground.cs`，修复乱码注释；顺手把 `cameraLefteEdge` 参数名拼写修正，`imageWidthOffset=10` 增加注释说明用途（或改名为 `loopOverlap` 之类语义化名字）。

### 🟢 轻微（L）— 风格/清理项

**L1. if 单行不换行系统性违规**（GameInput L70/75/80、AudioManager 10+ 处、HitSlashEffect、CinemaScreenShake、CounterShockwaveEffect、ParallaxLayer、UI_KeyRebind）——与 CLAUDE.md 规范冲突，建议 Rider 格式化规则落库后批量执行。

**L2. `UI_KeyRebind` 全文件无注释**——字段 `action/keyLabel/rebindButton`、`OnGUI` 捕获逻辑均无说明，直接违反注释规范；补充注释或作为规范检查示例。

**L3. 脚步声/跳跃/落地复用 `uiSource`**——`PlayFootstepSfx/PlayJumpSfx/PlayLandingSfx/PlayGoldPickupSfx/PlaySaveSfx` 等与按钮音效共用一源，OneShot 叠加时互相抢占、音量失衡；建议按语义拆分（角色动作源/UI 源/环境源），或引入 AudioMixer 分组。

**L4. 音量/键位为设备级且无迁移**——PlayerPrefs 无版本号；未来键格式或音量粒度（0-10 整数）变更会静默丢失；建议至少加 `Audio_SettingsVersion` 与 `GameInputBindingsVersion` 兜底迁移。

**L5. `PlayJumpSfx/PlayLandingSfx` 隐含「数组 2 元素」假设**（`Mathf.Min(1, len-1)`）——数组扩充到 3+ 时二段跳固定取 index1；建议改为显式字段（如 `jumpSfxClip`/`doubleJumpSfxClip` 各一）或文档化约束。

**L6. VFX 无对象池**——反击/暴击/落地等高频特效每发 Instantiate/Destroy，MVP 可接受，但建议 V2 引入通用特效池（项目已有 `ObjectPool` 类的话优先复用），并将 `HitSlashEffect` 的材质实例（`sr.material`）改为共享材质参数化或专用 Shader 属性块（MaterialPropertyBlock），避免每特效一份材质。

**L7. 冗余 import 与拼写**——`CinemaScreenShake.cs` 未使用的 `using System.Collections.Generic;`；`Entity_VFX`（关联文件）`System.Diagnostics`/`System.Xml.Linq` 未使用、注释乱码、`ChockVFX` 拼写——建议一次清理。

**L8. 中文字段标识符**（AudioManager 的 `环境/UI音效/角色/战斗/存档/打字机`）与英文混用——合法但风格不统一，建议统一英文（如 `environmentGroup/uiGroup/playerGroup/...`）或在项目规范中明确允许。

---

## 附：审查结论摘要

- **健康度**：结构清晰、职责基本单一，AudioManager 的 BGM 双源 crossfade/BGM 栈/协程竞态防护与 GameInput 的阻塞策略实现质量较高；但存在 2 个输入链功能性 Bug（S1/S2）与 3 处文档漂移，建议在 MVP 前修复 S 级并回写文档。
- **最高优先级动作**：修复 S1（键位冲突）与 S2（技能输入绕过阻塞）→ 回写三份文档 → 处理 M3（双轨按钮音效）。
