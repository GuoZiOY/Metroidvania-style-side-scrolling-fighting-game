using UnityEngine;

// 管理器容器引导：挂到 "各类系统和管理器" 根节点，
// 使容器及其子管理器（战利品/经验/任务/联机等）跨场景持久（方案A）。
// 子管理器各自 DontDestroyOnLoad 在个别对象上不可靠，由容器根统一保证。
public class ManagerContainer : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
