using UnityEngine;

namespace Networking
{
    // 网络行为基类。
    // 所有需要在网络间同步的 MonoBehaviour 都应继承此类。
    // 单机模式下 IsServer/IsClient 均为 false，OnNetworkSpawn/Despawn 不执行。
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        // 网络对象 ID（由服务端分配，全局唯一）
        public uint NetId { get; set; }

        // 此对象是否归本地玩家所有（本地操控的角色为 true）
        public bool IsOwner { get; set; }

        // ---- 网络状态判断 ----
        public bool IsServer => NetworkManager.Instance != null && NetworkManager.Instance.IsServer;
        public bool IsClient => NetworkManager.Instance != null && NetworkManager.Instance.IsClient;
        public bool IsHost => NetworkManager.Instance != null && NetworkManager.Instance.IsHost;
        public bool IsLocalPlayer => IsOwner;
        public bool IsNetworkActive => NetworkManager.Instance != null;

        // 获取此对象上的 NetworkObject 组件
        public NetworkObject NetworkObject
        {
            get
            {
                if (_netObj == null)
                    _netObj = GetComponent<NetworkObject>();
                return _netObj;
            }
        }
        private NetworkObject _netObj;

        // ---- 网络生命周期 ----

        // 对象在网络上生成时调用（服务端和客户端都会触发）
        public virtual void OnNetworkSpawn() { }

        // 对象从网络上销毁时调用
        public virtual void OnNetworkDespawn() { }
    }
}
