using UnityEngine;

// 全局相机宽高比锁定（letterbox）：用视口矩形把相机强制为设计宽高比（16:9），
// 屏幕更宽 → 左右黑边；更窄 → 上下黑边。相机永远只渲染设计视野，绝不超过关卡/confiner 范围。
// 运行时自动创建（无需场景摆放）；不影响 vcam 镜头（死亡聚焦等相机效果仍正常）。
public class CameraAspectLock : MonoBehaviour
{
    public static CameraAspectLock Instance { get; private set; }

    [SerializeField] private float designAspect = 16f / 9f; // 设计宽高比

    private Camera cam;
    private bool logged;

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

        float screenAspect = (float)Screen.width / Screen.height;
        if (screenAspect <= 0.01f)
            return;

        // 视口矩形：屏幕更宽 → 左右黑边；更窄 → 上下黑边
        if (screenAspect > designAspect)
        {
            float w = designAspect / screenAspect;
            cam.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f); // 左右黑边
        }
        else
        {
            float h = screenAspect / designAspect;
            cam.rect = new Rect(0f, (1f - h) * 0.5f, 1f, h); // 上下黑边
        }

        // 黑边用纯黑清屏色（仅首次设置）
        if (cam.clearFlags != CameraClearFlags.SolidColor)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        if (!logged)
        {
            logged = true;
            Debug.Log($"[CameraAspectLock] 生效 设计={designAspect:F2} 当前={Screen.width}x{Screen.height} rect={cam.rect}");
        }
    }
}
