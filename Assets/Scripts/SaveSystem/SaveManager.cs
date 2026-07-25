using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// 存档管理器（单例）。
// 负责存档/读档/删除/列举存档。
// 数据收集和恢复委托给各系统的方法，SaveManager 只做调度。
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const int MAX_SLOTS = 4;  // 4 个手动存档槽
    private const string FILE_PREFIX = "slot_";
    private const string PROFILES_FILE = "profiles.json";

    public string CurrentCheckpointId { get; set; }
    public int CurrentSlotIndex { get; set; } = -1;

    private float accumulatedPlayTime;  // 累计游玩时间（不含当前会话）
    private float sessionStartTime;     // 当前会话开始时间

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ItemLookup.Initialize();
        sessionStartTime = Time.time;
    }

    /// <summary>当前总游玩时间 = 累计 + 本局已玩</summary>
    private float TotalPlayTime => accumulatedPlayTime + (Time.time - sessionStartTime);

    // ==================== 公开 API ====================

    // 存档到当前槽位（检查点用）
    public void Save()
    {
        if (CurrentSlotIndex < 0)
        {
            Debug.LogError("[SaveManager] 未设置当前存档槽位 (CurrentSlotIndex)");
            return;
        }
        Save(CurrentSlotIndex);
    }

    // 存档到指定槽位
    public void Save(int slotIndex)
    {
        var data = CollectSaveData();
        if (data == null)
        {
            Debug.LogError("[SaveManager] 无法收集存档数据");
            return;
        }

        data.saveTime = DateTime.UtcNow.ToString("O");
        data.playTime = TotalPlayTime;
        data.lastCheckpointId = CurrentCheckpointId ?? "";
        string json = JsonUtility.ToJson(data, true);
        string path = GetSavePath(slotIndex);
        File.WriteAllText(path, json);

        UpdateProfile(slotIndex, data);
        Debug.Log($"[SaveManager] 存档成功 slot={slotIndex} scene={data.sceneName}");
    }

    // 读档
    public void Load(int slotIndex)
    {
        CurrentSlotIndex = slotIndex;

        string path = GetSavePath(slotIndex);
        if (!File.Exists(path))
        {
            Debug.LogError($"[SaveManager] 存档不存在 slot={slotIndex}");
            return;
        }

        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
        {
            Debug.LogError("[SaveManager] 存档解析失败");
            return;
        }

        // 如果场景不同，先切换场景
        string currentScene = SceneManager.GetActiveScene().name;
        if (data.sceneName != currentScene)
        {
            pendingLoad = data;
            SceneManager.sceneLoaded += OnSceneLoadedForLoad;
            SceneManager.LoadScene(data.sceneName);
        }
        else
        {
            ApplySaveData(data);
            RefreshAllUI();
        }
    }

    private SaveData pendingLoad;

    private void OnSceneLoadedForLoad(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForLoad;
        if (pendingLoad != null)
        {
            StartCoroutine(ApplySaveDataDelayed(pendingLoad));
            pendingLoad = null;
        }
    }

    private System.Collections.IEnumerator ApplySaveDataDelayed(SaveData data)
    {
        // 主动等待所有关键系统就绪（而非固定延时）
        float timeout = 5f;
        while (timeout > 0)
        {
            var player = FindAnyObjectByType<Player>();
            var invSys = FindAnyObjectByType<PlayerInventorySystem>();
            var sdm = FindAnyObjectByType<SkillDataManager>();
            var ssm = FindAnyObjectByType<SkillSlotManager>();
            if (player != null && invSys != null && sdm != null && ssm != null) break;
            yield return null;
            timeout -= Time.deltaTime;
        }
        Debug.Log($"[SaveManager] 系统就绪（等待了 {5f - timeout:F1}s），开始恢复数据");

        ApplySaveData(data);
        yield return new WaitForSeconds(0.1f);  // 短暂等待让装备/技能生效
        RefreshAllUI();
    }

    private SkillSaveData lastLoadedSkills;  // 缓存给 UI 刷新用

    private void RefreshAllUI()
    {
        // 刷新技能树（含未激活的面板——玩家可能没打开技能面板）
        if (lastLoadedSkills != null)
        {
            var skillTrees = Resources.FindObjectsOfTypeAll<UI_SkillTree>();
            foreach (var skillTree in skillTrees)
            {
                if (skillTree != null) skillTree.LoadSkillLevels(lastLoadedSkills.learned);
            }
        }

        // 刷新被动技能
        var psm = FindAnyObjectByType<PassiveSkillManager>();
        if (psm != null) psm.RefreshAllPassiveSkills();

        AudioManager.Instance?.PlayLoadSfx();

        Debug.Log("[SaveManager] UI 刷新完成");
    }

    // 删除存档
    public void Delete(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveManager] 已删除存档 slot={slotIndex}");
        }
        RemoveProfile(slotIndex);
    }

    // 读档并强制重载场景（死亡后继续游戏用，重置敌人/宝箱等）
    public void LoadWithReload(int slotIndex)
    {
        CurrentSlotIndex = slotIndex;

        string path = GetSavePath(slotIndex);
        if (!File.Exists(path))
        {
            Debug.LogError($"[SaveManager] 存档不存在 slot={slotIndex}");
            return;
        }

        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
        {
            Debug.LogError("[SaveManager] 存档解析失败");
            return;
        }

        pendingLoad = data;
        SceneManager.sceneLoaded += OnSceneLoadedForLoad;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 获取所有存档槽的元数据（主菜单展示）
    public List<SaveProfile> ListProfiles()
    {
        var profiles = LoadProfileList();
        return profiles.profiles;
    }

    // ==================== 数据收集 ====================

    private SaveData CollectSaveData()
    {
        Player player = FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogError("[SaveManager] 场景中无 Player");
            return null;
        }

        var data = new SaveData();
        data.sceneName = SceneManager.GetActiveScene().name;
        data.posX = player.transform.position.x;
        data.posY = player.transform.position.y;
        data.posZ = player.transform.position.z;
        // 角色数据
        data.player = CollectPlayerData(player);

        // 属性
        data.stats = CollectStatData(player);

        // 背包
        data.inventory = CollectInventory(player);

        // 装备
        data.equipment = CollectEquipment(player);

        // 技能
        data.skills = CollectSkillData();

        // 任务
        data.quests = CollectQuestData();

        return data;
    }

    private PlayerSaveData CollectPlayerData(Player player)
    {
        var d = new PlayerSaveData();
        d.currentHP = player.health != null ? player.health.GetCurrentHP() : 100;

        var inv = player.GetComponentInChildren<PlayerInventorySystem>();
        if (inv != null) d.currency = inv.GetCurrency();

        var lm = player.GetComponent<PlayerLevelManager>();
        if (lm != null)
        {
            d.currentLevel = lm.CurrentLevel;
            d.currentExp = lm.CurrentExp;
            d.unspentSkillPoints = lm.SkillPoints;
            d.unspentAttributePoints = lm.AttributePoints;
        }

        var spm = player.GetComponent<SkillPointManager>();
        if (spm != null)
        {
            d.totalSkillPoints = spm.TotalSkillPoints;
            d.usedSkillPoints = spm.UsedSkillPoints;
        }

        return d;
    }

    private List<StatSaveEntry> CollectStatData(Player player)
    {
        var list = new List<StatSaveEntry>();
        if (player.stats == null) return list;

        foreach (StatType st in Enum.GetValues(typeof(StatType)))
        {
            Stat stat = player.stats.GetStatByType(st);
            if (stat != null)
            {
                list.Add(new StatSaveEntry
                {
                    statType = (int)st,
                    baseValue = stat.GetBaseValue()
                });
            }
        }
        return list;
    }

    private List<InventorySlotData> CollectInventory(Player player)
    {
        var list = new List<InventorySlotData>();
        var invSys = FindAnyObjectByType<PlayerInventorySystem>();
        if (invSys == null) return list;
        Inventory_Player inv = invSys.GetInventory();
        if (inv == null) return list;

        foreach (var kvp in inv.itemDictionary)
        {
            Inventory_Item item = kvp.Value;
            if (item == null || item.itemData == null) continue;

            list.Add(new InventorySlotData
            {
                itemId = item.itemData.itemId,
                stackSize = item.currentStackSize,
                slotIndex = kvp.Key,
                rarity = item.actualRarity.HasValue ? (int)item.actualRarity.Value : (int)item.itemData.rarity,
                rarityMultiplier = item.rarityMultiplier > 0 ? item.rarityMultiplier : 1f,
            });
        }
        return list;
    }

    private List<EquipSlotData> CollectEquipment(Player player)
    {
        var list = new List<EquipSlotData>();
        var invSys = FindAnyObjectByType<PlayerInventorySystem>();
        if (invSys == null) return list;
        EquipmentSystem es = invSys.GetEquipmentSystem();
        if (es == null) return list;

        var equipped = es.GetEquippedItemsForSave();
        if (equipped == null) return list;

        foreach (var e in equipped)
        {
            list.Add(new EquipSlotData
            {
                itemId = e.item.itemData.itemId,
                itemType = (int)e.item.itemData.itemType,
                slotIndex = e.slotIndex,
                rarity = e.item.actualRarity.HasValue ? (int)e.item.actualRarity.Value : (int)e.item.itemData.rarity,
                rarityMultiplier = e.item.rarityMultiplier > 0 ? e.item.rarityMultiplier : 1f,
            });
        }
        return list;
    }

    private SkillSaveData CollectSkillData()
    {
        var d = new SkillSaveData();
        d.learned = new List<SkillLevelEntry>();
        var seenTypes = new HashSet<int>();

        // 从 Player_SkillManager 直接读取所有技能的等级（包括未装备槽位的技能）
        var psm = FindAnyObjectByType<Player_SkillManager>();
        if (psm != null && psm.allSkills != null)
        {
            foreach (var skill in psm.allSkills)
            {
                if (skill == null) continue;
                foreach (var kvp in skill.GetAllUpgradeTypeLevels())
                {
                    int upgType = (int)kvp.Key;
                    if (kvp.Value > 0 && !seenTypes.Contains(upgType))
                    {
                        seenTypes.Add(upgType);
                        d.learned.Add(new SkillLevelEntry { upgradeType = upgType, level = kvp.Value });
                    }
                }
            }
        }

        // 也合并 SkillDataManager 的数据（技能树里升级但未绑定槽位的）
        var sdm = SkillDataManager.Instance ?? FindAnyObjectByType<SkillDataManager>();
        if (sdm != null)
        {
            foreach (var kvp in sdm.GetAllSkillLevels())
            {
                int upgType = (int)kvp.Key;
                if (kvp.Value > 0 && !seenTypes.Contains(upgType))
                {
                    seenTypes.Add(upgType);
                    d.learned.Add(new SkillLevelEntry { upgradeType = upgType, level = kvp.Value });
                }
            }
        }

        // 槽位绑定
        var ssm = SkillSlotManager.Instance ?? FindAnyObjectByType<SkillSlotManager>();
        if (ssm != null)
        {
            d.slotBindings = new int[5];
            for (int i = 0; i < 5; i++)
                d.slotBindings[i] = (int)ssm.GetUpgradeTypeInSlot(i);
        }

        Debug.Log($"[SaveManager] 保存技能 {d.learned.Count} 个, 槽位绑定 {d.slotBindings?.Length ?? 0} 个");
        return d;
    }

    private QuestSaveData CollectQuestData()
    {
        var d = new QuestSaveData();
        QuestManager qm = QuestManager.Instance ?? FindAnyObjectByType<QuestManager>();
        if (qm == null) return d;

        var stageIds = qm.GetActiveQuestStageIdsForSave();
        d.active = new List<QuestSaveEntry>();
        foreach (var kvp in qm.GetActiveQuestsForSave())
        {
            var stageId = stageIds.TryGetValue(kvp.Key, out var sid) ? sid : "";
            var entry = new QuestSaveEntry { questId = kvp.Key, currentStageId = stageId };
            entry.objectiveProgress = kvp.Value;
            entry.objectives = new List<QuestObjectiveData>();
            if (kvp.Value != null)
            {
                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    entry.objectives.Add(new QuestObjectiveData
                    {
                        index = i,
                        currentCount = kvp.Value[i]
                    });
                }
            }
            d.active.Add(entry);
        }

        d.readyToClaim = qm.GetReadyToClaimQuestsForSave();
        d.completed = qm.GetCompletedQuestsForSave();
        d.failed = qm.GetFailedQuestsForSave();
        d.trackedQuestId = qm.GetTrackedQuestIdForSave();

        return d;
    }

    // ==================== 数据恢复 ====================

    private void ApplySaveData(SaveData data)
    {
        Player player = FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogError("[SaveManager] 读档失败：场景中无 Player");
            return;
        }

        // 恢复游玩时间
        accumulatedPlayTime = data.playTime;
        sessionStartTime = Time.time;

        // 位置
        player.transform.position = new Vector3(data.posX, data.posY, data.posZ);

        // 角色数据
        ApplyPlayerData(player, data.player);

        // 属性
        ApplyStatData(player, data.stats);

        // 背包（先清空再重建）
        ApplyInventory(player, data.inventory);

        // 装备
        ApplyEquipment(player, data.equipment);

        // 技能
        ApplySkillData(data.skills);

        // 任务
        ApplyQuestData(data.quests);

        // 存档点
        CurrentCheckpointId = data.lastCheckpointId;

        Debug.Log($"[SaveManager] 读档完成 slot scene={data.sceneName}");
    }

    private void ApplyPlayerData(Player player, PlayerSaveData d)
    {
        if (d == null) return;

        player.health?.SetCurrentHP(d.currentHP);

        var inv = player.GetComponentInChildren<PlayerInventorySystem>();
        if (inv != null) inv.SetCurrency(d.currency);

        var lm = player.GetComponent<PlayerLevelManager>();
        if (lm != null)
        {
            lm.LoadFromSave(d.currentLevel, d.currentExp, d.unspentSkillPoints, d.unspentAttributePoints);
        }

        var spm = player.GetComponent<SkillPointManager>();
        if (spm != null)
        {
            spm.LoadFromSave(d.totalSkillPoints, d.usedSkillPoints);
        }
    }

    private void ApplyStatData(Player player, List<StatSaveEntry> stats)
    {
        if (stats == null || player.stats == null) return;
        foreach (var entry in stats)
        {
            Stat stat = player.stats.GetStatByType((StatType)entry.statType);
            stat?.SetBaseValue(entry.baseValue);
        }
    }

    private void ApplyInventory(Player player, List<InventorySlotData> items)
    {
        var invSys = FindAnyObjectByType<PlayerInventorySystem>();
        if (invSys == null) return;
        Inventory_Player inv = invSys.GetInventory();
        if (inv == null || items == null) return;

        // 清空现有背包
        var occupied = new List<int>(inv.itemDictionary.Keys);
        foreach (int slot in occupied)
            inv.RemoveItemAtSlot(slot);

        // 重建物品
        foreach (var slot in items)
        {
            ItemDataSo itemData = ItemLookup.Find(slot.itemId);
            if (itemData == null) continue;

            LootRarity rarity = (LootRarity)slot.rarity;
            float multiplier = slot.rarityMultiplier > 0 ? slot.rarityMultiplier : 1f;
            var looted = new LootedItem(itemData, rarity) { statMultiplier = multiplier };
            Inventory_Item item = new Inventory_Item(looted);
            item.currentStackSize = Mathf.Max(1, slot.stackSize);
            inv.AddItem(item, slot.slotIndex);
        }
    }

    private void ApplyEquipment(Player player, List<EquipSlotData> equipment)
    {
        if (equipment == null || equipment.Count == 0) return;
        var invSys = FindAnyObjectByType<PlayerInventorySystem>();
        Debug.Log($"[SaveManager] ApplyEquipment: invSys={invSys != null} count={equipment.Count}");
        if (invSys == null) return;
        EquipmentSystem es = invSys.GetEquipmentSystem();
        Inventory_Player inv = invSys.GetInventory();
        Debug.Log($"[SaveManager] ApplyEquipment: es={es != null} inv={inv != null} invCount={inv?.itemDictionary.Count}");
        if (es == null || inv == null) return;

        es.UnequipAllForSave();

        // 装备物品不在背包里，需要直接创建然后装备
        foreach (var e in equipment)
        {
            ItemDataSo itemData = ItemLookup.Find(e.itemId);
            if (itemData == null) { Debug.LogWarning($"[SaveManager] Equip itemId={e.itemId} not found"); continue; }

            LootRarity rarity = (LootRarity)e.rarity;
            float multiplier = e.rarityMultiplier > 0 ? e.rarityMultiplier : 1f;
            var looted = new LootedItem(itemData, rarity) { statMultiplier = multiplier };
            Inventory_Item item = new Inventory_Item(looted);

            es.TryEquipItemToSlot(item, e.slotIndex);
            Debug.Log($"[SaveManager] Equip restored: {e.itemId} slot={e.slotIndex}");
        }
    }

    private void ApplySkillData(SkillSaveData d)
    {
        if (d == null) return;
        lastLoadedSkills = d;

        // 技能等级：先从 Resources 加载所有 Skill_DataSo，建立 upgradeType → Skill_DataSo 映射
        var skillDataMap = new Dictionary<SkillUpgradeType, Skill_DataSo>();
        var allSkillData = Resources.LoadAll<Skill_DataSo>("Data/StillData");
        foreach (var sd in allSkillData)
        {
            if (sd.upgradeType != SkillUpgradeType.None && !skillDataMap.ContainsKey(sd.upgradeType))
                skillDataMap[sd.upgradeType] = sd;
        }

        var sdm = SkillDataManager.Instance ?? FindAnyObjectByType<SkillDataManager>();
        Debug.Log($"[SaveManager] ApplySkillData: sdm={sdm != null} learnedCount={d.learned?.Count}");
        if (sdm != null && d.learned != null)
        {
            foreach (var entry in d.learned)
            {
                var upgradeType = (SkillUpgradeType)entry.upgradeType;
                if (skillDataMap.TryGetValue(upgradeType, out var skillData))
                {
                    sdm.UpdateSkillData(upgradeType, skillData, entry.level);
                }
            }
        }

        // 槽位绑定
        var ssm = SkillSlotManager.Instance ?? FindAnyObjectByType<SkillSlotManager>();
        if (ssm != null && d.slotBindings != null)
        {
            for (int i = 0; i < d.slotBindings.Length && i < 5; i++)
            {
                var upgradeType = (SkillUpgradeType)d.slotBindings[i];
                if (upgradeType != SkillUpgradeType.None)
                    ssm.BindSkillToSlot(upgradeType, i);
            }
        }

    }

    private void ApplyQuestData(QuestSaveData d)
    {
        QuestManager qm = QuestManager.Instance ?? FindAnyObjectByType<QuestManager>();
        if (qm == null || d == null) return;
        qm.LoadFromSave(d);
    }

    // ==================== Profile 管理 ====================

    private string GetSavePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"{FILE_PREFIX}{slotIndex}.json");
    }

    private string GetProfilesPath()
    {
        return Path.Combine(Application.persistentDataPath, PROFILES_FILE);
    }

    private SaveProfileList LoadProfileList()
    {
        string path = GetProfilesPath();
        if (!File.Exists(path)) return new SaveProfileList();
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveProfileList>(json) ?? new SaveProfileList();
    }

    private void SaveProfileList(SaveProfileList list)
    {
        File.WriteAllText(GetProfilesPath(), JsonUtility.ToJson(list, true));
    }

    private void UpdateProfile(int slotIndex, SaveData data)
    {
        var list = LoadProfileList();
        var profile = list.profiles.Find(p => p.slotIndex == slotIndex);
        if (profile == null)
        {
            profile = new SaveProfile();
            list.profiles.Add(profile);
        }
        profile.slotIndex = slotIndex;
        profile.saveTime = data.saveTime;
        profile.playTime = data.playTime;
        profile.sceneName = data.sceneName;
        profile.playerLevel = data.player?.currentLevel ?? 1;
        profile.isEmpty = false;
        SaveProfileList(list);
    }

    private void RemoveProfile(int slotIndex)
    {
        var list = LoadProfileList();
        list.profiles.RemoveAll(p => p.slotIndex == slotIndex);
        SaveProfileList(list);
    }
}
