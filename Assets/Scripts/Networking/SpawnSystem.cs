using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    // 网络对象生成/销毁系统。
    // 服务端调用 Spawn/Despawn，自动同步到所有客户端。
    public class SpawnSystem : MonoBehaviour
    {
        [Header("预制体注册表")]
        [SerializeField] private NetworkPrefabRegistry prefabRegistry;

        // 本地的 NetId → NetworkObject 映射
        private readonly Dictionary<uint, NetworkObject> _netObjects = new();

        // NetId 自增（服务端分配）
        private uint _nextNetId = 1;

        private NetworkManager Net => NetworkManager.Instance;

        // ==================== 服务端 API ====================

        // 在服务端生成一个网络对象，广播到所有客户端
        public NetworkObject Spawn(string prefabId, Vector3 position, Quaternion rotation, int ownerConnectionId = -1)
        {
            if (!Net.IsServer)
            {
                Debug.LogWarning("[SpawnSystem] 只有服务端可以生成网络对象");
                return null;
            }

            GameObject prefab = prefabRegistry.GetPrefab(prefabId);
            if (prefab == null)
            {
                Debug.LogError($"[SpawnSystem] 预制体 '{prefabId}' 未在注册表中找到");
                return null;
            }

            // 在服务端实例化
            GameObject instance = Instantiate(prefab, position, rotation);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[SpawnSystem] 预制体 '{prefabId}' 缺少 NetworkObject 组件");
                Destroy(instance);
                return null;
            }

            // 分配 NetId
            uint netId = _nextNetId++;
            netObj.OnSpawn(netId, ownerConnectionId);
            _netObjects[netId] = netObj;

            // 广播给所有客户端
            BroadcastSpawn(netId, prefabId, position, rotation, ownerConnectionId);

            Debug.Log($"[SpawnSystem] 生成 {prefabId} NetId={netId}");
            return netObj;
        }

        // 销毁一个网络对象
        public void Despawn(NetworkObject netObj)
        {
            if (!Net.IsServer)
            {
                Debug.LogWarning("[SpawnSystem] 只有服务端可以销毁网络对象");
                return;
            }

            if (netObj == null) return;

            netObj.OnDespawn();
            BroadcastDespawn(netObj.NetId);
            _netObjects.Remove(netObj.NetId);
            Destroy(netObj.gameObject);

            Debug.Log($"[SpawnSystem] 销毁 NetId={netObj.NetId}");
        }

        // ==================== 客户端处理（由 NetworkManager 消息路由调用） ====================

        // 收到 SpawnEntityMessage 后调用
        public void OnSpawnMessage(uint netId, string prefabId, Vector3 position, Quaternion rotation, int ownerConnectionId)
        {
            GameObject prefab = prefabRegistry.GetPrefab(prefabId);
            if (prefab == null)
            {
                Debug.LogError($"[SpawnSystem] 收到生成消息但预制体 '{prefabId}' 未找到");
                return;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[SpawnSystem] 客户端实例化 '{prefabId}' 缺少 NetworkObject");
                Destroy(instance);
                return;
            }

            netObj.OnSpawn(netId, ownerConnectionId);
            _netObjects[netId] = netObj;
        }

        // 收到 DespawnEntityMessage 后调用
        public void OnDespawnMessage(uint netId)
        {
            if (_netObjects.TryGetValue(netId, out NetworkObject netObj))
            {
                netObj.OnDespawn();
                _netObjects.Remove(netId);
                Destroy(netObj.gameObject);
            }
        }

        // ==================== 查询 ====================

        public NetworkObject GetNetObject(uint netId)
        {
            _netObjects.TryGetValue(netId, out NetworkObject obj);
            return obj;
        }

        public bool HasNetObject(uint netId) => _netObjects.ContainsKey(netId);

        // ==================== 内部 ====================

        private void BroadcastSpawn(uint netId, string prefabId, Vector3 position, Quaternion rotation, int ownerConnectionId)
        {
            // TODO: 阶段二/三接入 MessageDispatcher 和消息协议后实现
            // 示例：
            // var msg = new SpawnEntityMessage { NetId = netId, PrefabId = prefabId, ... };
            // byte[] packet = MessageDispatcher.PackMessage(MSG_SPAWN, msg);
            // Net.Broadcast(packet, true);
            Debug.Log($"[SpawnSystem] 广播生成: NetId={netId} Prefab={prefabId}");
        }

        private void BroadcastDespawn(uint netId)
        {
            // TODO: 阶段二/三实现消息协议后补充
            Debug.Log($"[SpawnSystem] 广播销毁: NetId={netId}");
        }
    }
}
