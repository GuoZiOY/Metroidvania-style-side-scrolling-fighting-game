---
name: equipment-affix-importer-mismatch
description: 新词缀CSV格式与现有 EquipmentAffixCSVImporter/数据模型/生成器 不匹配，需 systems-designer 跟进
metadata:
  type: project
---

新 6 列词缀 CSV 格式（含每段独立 min:max:pct、weight 列）与现有实现不匹配，是待办项：

- `Editor/EquipmentAffixCSVImporter.cs`：仍按旧 8 列解析（`f[5]/f[6]/f[7]`=min/max/isPct，`f[4]`=纯属性名列表），对新格式会越界/误解析，weight 硬编码为 5。
- `Data/EquipmentAffixDatabase.cs` 的 `EquipmentAffixEntry`：单组 `minValue/maxValue/isPercentage`，无法表达多段不同 pct 的条目（如双刃「狂战」=物伤8-15% + 护甲-10~-5固定）。
- `Others/ItemSystem/EquipmentAffixGenerator.cs` AddWeightedAffixes：用 `entry.minValue/maxValue/isPercentage` 生成 Modifier，需改为按段展开。
- `Data/AffixSelector.cs`：当前只过滤 `tier > maxTier`，未实现"下限=稀有度-1"的保底下界；若执行该约束需加 minTier 参数。

**Why:** 用户要求 CSV 直接覆盖写为新格式；经济策划职责不含写实现代码，故 importer/模型/生成器改动留待 systems-designer。

**How to apply:** 任何"导入词缀CSV"菜单操作在当前代码下会产出错误数据库 SO；在 importer 更新前，`Resources/Data/EquipmentAffixDatabase.asset` 保持旧数据。若 xlsx 仍是数据主源，`sync_xlsx_to_csv.ps1` 会以旧格式覆盖本 CSV。

关联：[[project-affix-redesign]]
