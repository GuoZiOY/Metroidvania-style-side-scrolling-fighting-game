using Cinemachine;
using UnityEngine;

// 全局相机宽高比锁定：按屏幕宽高比调整 vcam 正交尺寸，保持水平视野与设计一致，
// 避免打包/自由窗口/全屏在非设计宽高比下相机横向超出关卡/confiner 范围
// 运行时自动创建（无需场景摆放）；仅当宽高比偏离设计时才调整（不干扰设计比例下的相机缩放）
public class CameraAspectLock : MonoBehaviour
{
    public static CameraAspectLock Instance { get; private set; }

    [SerializeField] private float designAspect = 16f / 9f; // 设计宽高比

    private Camera cam;                       // 当前主相机
    private CinemachineVirtualCamera vcam;    // 当前场景激活 vcam
    private float designOrtho;                // 当前场景设计正交尺寸（首次捕获）
    private CinemachineVirtualCamera capturedVcam; // 已捕获的 vcam（切场景重捕）
    private bool logged;                      // 是否已打印生效日志

    // 游戏启动自动创建持久对象（打包/编辑器 Play 都生效）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<CameraAspectLock>() == null)
            new GameObject("CameraAspectLock").AddComponent<CameraAspectLock>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;
        if (cam == null || cam.orthographic == false)
            return;

        // 惰性查找当前场景激活 vcam；切场景后 vcam 变化 → 重新记录设计正交尺寸
        if (vcam == null)
            vcam = FindActiveVcam();
        if (vcam != capturedVcam)
        {
            designOrtho = vcam != null ? vcam.m_Lens.OrthographicSize : cam.orthographicSize;
            capturedVcam = vcam;
        }

        float actualAspect = (float)Screen.width / Screen.height;
        if (actualAspect <= 0.01f)
            return;

        // 保持水平视野 = 设计值：targetOrtho = designOrtho × designAspect / actualAspect
        // 只缩不放（Mathf.Min）：宽屏缩正交保持水平不溢出；窄屏保持设计正交（横向少看），绝不放大避免纵向溢出
        float targetOrtho = Mathf.Min(designOrtho * designAspect / actualAspect, designOrtho);
        if (Mathf.Abs(targetOrtho - designOrtho) < 0.01f)
            return; // 宽高比等于设计 → 不调整（不干扰设计比例下的相机缩放/死亡聚焦）

        if (vcam != null)
            vcam.m_Lens.OrthographicSize = targetOrtho;
        else
            cam.orthographicSize = targetOrtho;

        if (!logged)
        {
            logged = true;
            Debug.Log($"[CameraAspectLock] 生效 设计宽高比={designAspect:F2} 当前={Screen.width}x{Screen.height} ortho={designOrtho:F1}->{targetOrtho:F1}");
        }
    }

    // 找场景激活 vcam（Priority>0 优先；没有则第一个）
    private CinemachineVirtualCamera FindActiveVcam()
    {
        var vcams = FindObjectsByType<CinemachineVirtualCamera>(FindObjectsSortMode.None);
        var active = System.Array.Find(vcams, v => v != null && v.Priority > 0);
        return active != null ? active : (vcams.Length > 0 ? vcams[0] : null);
    }
}
