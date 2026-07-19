using UnityEngine;

namespace Networking
{
    // 网络对象组件。挂载在每个需要网络同步的预制体根节点上。
    // 持有 NetId、预制体标识，自动收集子节点中的所有 NetworkBehaviour。
    public class NetworkObject : MonoBehaviour
    {
        [Tooltip("预制体标识，必须与 NetworkPrefabRegistry 中的条目一致")]
        public string prefabId;

        // 网络对象 ID（服务端分配）
        public uint NetId { get; set; }

        // 拥有此对象的客户端连接 ID（-1 表示无所有者）
        public int OwnerConnectionId { get; set; } = -1;

        // 此对象上的所有 NetworkBehaviour 组件
        public NetworkBehaviour[] Behaviours
        {
            get
            {
                if (_behaviours == null || _behaviours.Length == 0)
                    _behaviours = GetComponentsInChildren<NetworkBehaviour>(true);
                return _behaviours;
            }
        }
        private NetworkBehaviour[] _behaviours;

        // 生成时由 SpawnSystem 调用，初始化所有 NetworkBehaviour
        public void OnSpawn(uint netId, int ownerConnectionId = -1)
        {
            NetId = netId;
            OwnerConnectionId = ownerConnectionId;

            foreach (var beh in Behaviours)
            {
                beh.NetId = netId;
                beh.IsOwner = ownerConnectionId != -1 &&
                              NetworkManager.Instance != null &&
                              ownerConnectionId == GetLocalConnectionId();
                beh.OnNetworkSpawn();
            }
        }

        // 销毁时由 SpawnSystem 调用
        public void OnDespawn()
        {
            foreach (var beh in Behaviours)
                beh.OnNetworkDespawn();
        }

        private static int GetLocalConnectionId()
        {
            // TODO: 在阶段三接入玩家连接管理后实现
            // 暂时返回 -1 表示无法确定
            return -1;
        }
    }
}
