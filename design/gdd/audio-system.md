---
status: reverse-documented
source: Assets/Scripts/AudioSystem/
date: 2026-07-28
verified-by: oy
---

# 音频系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。

---

## 1. AudioManager 架构

```
AudioManager (单例, MonoBehaviour)
  音频分组 (Inspector 赋值):

  BGM:            bgmClip + volume
  环境:            宝箱音效
  UI:             按钮点击、按钮拒绝
  玩家:            脚步、跳跃(数组)、落地(数组)、跳跃攻击额外音效
  战斗:            挥砍(数组)、命中(数组)、额外(数组)、暴击(数组)、
                  反击成功(数组)、反击命中
  存档:            存档音效、读档音效
  打字机:          对话打字音效
```

### API

```
PlayButtonSfx()    — UI 按钮点击
PlayDenySfx()      — UI 按钮拒绝
PlaySaveSfx()      — 存档成功
PlayLoadSfx()      — 读档完成

PlayHitSfx()       — 普通命中
PlayCritSfx()      — 暴击命中
PlayCounterSuccessSfx() — 反击成功
PlayCounterHitSfx()     — 反击命中

PlayJumpSfx()      — 随机选一个跳跃音效
PlayLandingSfx()   — 随机选一个落地音效
```

数组音效随机选择: `clips[Random.Range(0, clips.Length)]`

---

## 2. UI_ButtonSfx

```
简单组件: 挂到 Button 上 → onClick 时自动调用 AudioManager.Instance.PlayButtonSfx()
用途: 统一的 UI 按钮反馈音效
```

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
