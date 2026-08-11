using UnityEngine;

// 简单精灵帧动画播放器：在 Inspector 手动拖入一组精灵图片（frames 数组），按帧率逐帧播放。
// 支持：启动自动播放/循环/单次、播放/暂停/停止、运行时替换帧序列。
[RequireComponent(typeof(SpriteRenderer))]
public class SimpleFrameAnimator : MonoBehaviour
{
    [Header("动画帧")]
    [SerializeField] private Sprite[] frames; // 手动拖入的精灵帧序列（按数组顺序播放）
    [SerializeField] private float fps = 10f; // 播放帧率（每秒帧数）

    [Header("播放行为")]
    [SerializeField] private bool playOnAwake = true; // 启动时自动播放
    [SerializeField] private bool loop = true;        // 是否循环（false=播放到最后一帧停止）
    [SerializeField] private bool resetOnPlay = true; // 每次 Play() 是否从第 0 帧开始

    public bool isPlaying { get; private set; }  // 是否正在播放
    public int CurrentFrame => currentFrame;     // 当前帧索引

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float timer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (playOnAwake)
            Play();
        else if (frames != null && frames.Length > 0)
            ApplyFrame(0); // 未播放也先显示第 0 帧，避免空白
    }

    private void Update()
    {
        if (isPlaying == false || frames == null || frames.Length == 0)
            return;

        // 按帧率累积计时，跨越一帧则推进一帧（大延迟时可一次补多帧）
        timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.001f, fps);
        while (timer >= interval)
        {
            timer -= interval;
            AdvanceFrame();
        }
    }

    // 推进到下一帧：处理循环 / 单次停止
    private void AdvanceFrame()
    {
        int next = currentFrame + 1;
        if (next >= frames.Length)
        {
            if (loop)
                next = 0;
            else
            {
                isPlaying = false;
                return;
            }
        }
        ApplyFrame(next);
    }

    // 把指定帧应用到 SpriteRenderer
    private void ApplyFrame(int index)
    {
        currentFrame = index;
        if (spriteRenderer != null && frames != null && index >= 0 && index < frames.Length)
            spriteRenderer.sprite = frames[index];
    }

    // 开始播放（按 resetOnPlay 决定从第 0 帧还是当前帧继续）
    public void Play()
    {
        if (frames == null || frames.Length == 0)
            return;
        if (resetOnPlay || isPlaying == false)
            ApplyFrame(0);
        timer = 0f;
        isPlaying = true;
    }

    // 暂停播放（停留在当前帧）
    public void Pause()
    {
        isPlaying = false;
    }

    // 停止播放并回到第 0 帧
    public void Stop()
    {
        isPlaying = false;
        ApplyFrame(0);
    }

    // 设置播放帧率
    public void SetFps(float value)
    {
        fps = Mathf.Max(0.01f, value);
    }

    // 运行时替换帧序列（从第 0 帧重新显示）
    public void SetFrames(Sprite[] newFrames)
    {
        frames = newFrames;
        ApplyFrame(0);
    }
}
