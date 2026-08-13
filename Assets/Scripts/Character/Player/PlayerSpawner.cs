using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// 玩家生成器（跨场景持久单例）— 方案A：玩家对象唯一且跨场景保留
// 场景内不再放置 Player，改为由本生成器在场景加载时生成/保留持久玩家，
// 每次进入场景先把玩家放到该场景入口存档点（安全默认；读档时 ApplySaveData 会覆盖为存档位置），
// 并把场景的虚拟相机绑定到持久玩家。
public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    [Header("玩家预制体")]
    [SerializeField] private GameObject playerPrefab; // 玩家预制体（需在场景外预先转换好）

    private GameObject persistentPlayer; // 跨场景保留的玩家实例
    private static string arrivePortalId;  // 到达场景后要定位的传送门 ID（传送门对传送门；空=入口存档点）

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 传送门调用：到达目标场景入口存档点（存档只由 saveBeforeTeleport 负责，无到达后重存）
    public static void MarkSpawnAtEntry()
    {
        arrivePortalId = null; // 走入口存档点
    }

    // 传送门调用：到达目标场景的指定传送门（传送门对传送门；portalId 为空则回入口存档点）
    public static void MarkSpawnAtPortal(string portalId)
    {
        arrivePortalId = portalId;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isMenu = scene.name == "主菜单";

        // 进入游戏场景才生成玩家（主菜单不预生成，避免玩家系统在菜单中提前初始化）
        if (persistentPlayer == null && !isMenu)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] 未设置玩家预制体 playerPrefab");
                return;
            }
            persistentPlayer = Instantiate(playerPrefab);
            DontDestroyOnLoad(persistentPlayer);
        }

        // 主菜单场景隐藏玩家，避免出现在菜单画面中；进入游戏场景再显示
        if (persistentPlayer != null)
            persistentPlayer.SetActive(!isMenu);

        // 绑定场景虚拟相机到持久玩家（场景内相机无法在编辑期绑定到跨场景对象）
        BindCameraToPlayer();

        // 每次进入场景先定位到入口检查点（读档时 ApplySaveData 会覆盖为存档位置）
        StartCoroutine(PlaceAtEntryPoint());
    }

    // 把场景内的虚拟相机绑定到持久玩家
    private void BindCameraToPlayer()
    {
        if (persistentPlayer == null) return;

        // 双 vcam 兼容：按命名约定选 follow vcam（Intro vcam 名含 "Intro" 且 priority=0，不绑定）
        var vcams = FindObjectsByType<CinemachineVirtualCamera>();
        var vcam = System.Array.Find(vcams, v => v != null && v.name.Contains("Follow"));
        if (vcam == null)
            vcam = System.Array.Find(vcams, v => v != null && v.Priority > 0);

        if (vcam != null)
        {
            vcam.Follow = persistentPlayer.transform;
            vcam.LookAt = persistentPlayer.transform;
        }
    }

    // 定位到场景入口检查点（isEntryPoint 优先，兜底用第一个检查点），随后按需重新存档。
    // 若传送门标记了目标传送门 ID，则优先定位到该传送门（找不到回退入口存档点）。
    private System.Collections.IEnumerator PlaceAtEntryPoint()
    {
        yield return null; // 等一帧，确保场景对象就绪

        if (persistentPlayer == null) yield break;

        // 传送门对传送门：优先定位到目标场景的指定传送门
        if (!string.IsNullOrEmpty(arrivePortalId))
        {
            string portalId = arrivePortalId;
            arrivePortalId = null; // 一次性消费
            if (TryPlaceAtPortal(portalId))
            {
                yield break;
            }
            // 未找到目标传送门 → 回退到入口存档点
        }

        var checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        Checkpoint entry = null;
        foreach (var c in checkpoints)
        {
            if (c.IsEntryPoint) { entry = c; break; }
        }
        if (entry == null && checkpoints.Length > 0)
            entry = checkpoints[0];

        if (entry != null)
            persistentPlayer.transform.position = entry.RespawnPosition;
    }

    // 把玩家放到指定 ID 的传送门落点（找不到返回 false，由调用方回退入口存档点）
    private bool TryPlaceAtPortal(string portalId)
    {
        var portals = FindObjectsByType<Portal>(FindObjectsSortMode.None);
        foreach (var p in portals)
        {
            if (p != null && p.PortalId == portalId)
            {
                persistentPlayer.transform.position = p.SpawnPosition;
                return true;
            }
        }
        return false;
    }
}
