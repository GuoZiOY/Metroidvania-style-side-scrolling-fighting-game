using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 场景切换过场「电影化开幕/退幕」：
//   画面变灰+黑边同时进入 → 区域名浮出（一直存在）→ 画面变黑（名字依旧，黑幕从四周向中心反向淡入，颜色由灰渐变到黑）
//   → 黑幕盖满时黑边+灰罩淡出（名字保留，在黑幕上停留）→ 异步加载完成/激活新场景
//   → 中心透明淡出显现（不愈合，名字随中心淡出一同淡化）→ 结束
// 关键：使用 LoadSceneAsync 异步加载，加载全程在后台进行、不阻塞主线程，
// 并在黑幕遮住时手动激活新场景 → 激活瞬间的卡顿不可见，整体顺滑无顿挫。
// 过渡层在场景中预先生成（默认关闭），便于在 Inspector 直接调区域名字体；未生成时自动创建兜底。
public class SceneTransitionFader : MonoBehaviour
{
    private static SceneTransitionFader instance;
    public static SceneTransitionFader Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("SceneTransitionFader");
                instance = go.AddComponent<SceneTransitionFader>();
            }
            return instance;
        }
    }

    [Header("阶段时长（秒）")]
    [SerializeField] private float grayBarDuration = 0.5f;    // 画面变灰 + 黑边同时进入时长（期间后台加载）
    [SerializeField] private float nameInDuration = 0.5f;     // 名字浮出时长（之后一直存在）
    [SerializeField] private float blackCoverDuration = 0.5f; // 画面变黑时长（名字依旧存在）
    [SerializeField] private float nameBarOutDuration = 0.4f; // 黑幕盖满时，名字+黑边+灰罩一起淡出时长
    [SerializeField] private float revealDuration = 0.8f;     // 中心透明淡出时长（此时黑边/名字已淡出，黑幕从中心向外消失）
    [SerializeField] private float readyBuffer = 0f;          // 新场景激活后就绪缓冲（设 0=激活后立即中心淡出）

    [Header("黑边")]
    [SerializeField] private float barHeight = 120f;          // 黑边高度

    // 区域名映射：场景名 → 显示名（可自行增改）
    private static readonly Dictionary<string, string> AreaNames = new Dictionary<string, string>
    {
        { "level0", "破落之墟" },
        { "level1", "崩塌回廊" },
    };

    private GameObject overlayGo;      // 整个过渡层（默认关闭，过渡时激活）
    private Material shatterMat;       // 黑幕 shader 材质（中心淡出用，可能为 null → 回退 CanvasGroup 均匀淡出）
    private CanvasGroup shatterCg;     // 黑幕层透明度（画面变黑时淡入）
    private Image shatterImage;        // 黑幕层 Image
    private Image grayWashImage;       // 灰色滤罩（画面变灰时淡入，黑幕盖屏后撤下）
    private RectTransform topBar;      // 上黑边（位移）
    private RectTransform bottomBar;   // 下黑边（位移）
    private Image topBarImg;           // 上黑边 Image（黑幕盖满时淡出用）
    private Image bottomBarImg;        // 下黑边 Image（黑幕盖满时淡出用）
    private TextMeshProUGUI areaName;  // 区域名文字
    private bool isTransitioning;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        FindOrSetupOverlay();
        WarmUpShader(); // 预热 shader，避免首次使用时编译卡顿
    }

    // 优先复用场景中预先生成的过渡层（默认关闭）；否则运行时自建兜底
    private void FindOrSetupOverlay()
    {
        var found = transform.Find("SceneTransitionOverlay");
        if (found != null)
        {
            overlayGo = found.gameObject;
            CacheOverlayRefs();
            return;
        }
        BuildOverlay();
    }

    // 从场景生成的过渡层里缓存各元素引用
    private void CacheOverlayRefs()
    {
        foreach (var img in overlayGo.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "Shatter") shatterImage = img;
            else if (img.name == "TopBar") { topBar = img.rectTransform; topBarImg = img; }
            else if (img.name == "BottomBar") { bottomBar = img.rectTransform; bottomBarImg = img; }
            else if (img.name == "GrayWash") grayWashImage = img;
            else if (img.name == "Vignette") img.color = new Color(0, 0, 0, 0); // 旧版暗角弃用，直接隐藏防干扰
        }
        // 场景过渡层可能是旧版生成的（无 GrayWash），缺少则运行时补建
        if (grayWashImage == null)
            CreateGrayWash();
        foreach (var cg in overlayGo.GetComponentsInChildren<CanvasGroup>(true))
            if (cg.name == "Shatter") shatterCg = cg;
        foreach (var t in overlayGo.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.name == "AreaName") areaName = t;

        if (shatterImage != null)
        {
            var shader = Shader.Find("Custom/ShatterTransition");
            if (shader != null)
            {
                if (shatterImage.material == null || shatterImage.material.shader != shader)
                {
                    shatterMat = new Material(shader);
                    shatterImage.material = shatterMat;
                }
                else
                {
                    shatterMat = shatterImage.material;
                }
            }
            else
            {
                shatterImage.color = Color.black;
            }
        }
    }

    // 预热 shader：启动时渲染一次，让黑幕 shader 编译完成，避免过场首次使用的顿卡
    private void WarmUpShader()
    {
        if (shatterMat == null) return;
        var warmRt = new RenderTexture(8, 8, 0);
        Graphics.Blit(Texture2D.whiteTexture, warmRt, shatterMat);
        RenderTexture.active = null; // 先解除 active，再释放，避免 "Releasing active RT" 警告
        warmRt.Release();
    }

    // 运行时自建过渡层兜底
    private void BuildOverlay()
    {
        overlayGo = new GameObject("SceneTransitionOverlay");
        overlayGo.transform.SetParent(transform);

        var canvas = overlayGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        overlayGo.AddComponent<GraphicRaycaster>();

        CreateGrayWash();
        CreateShatter();
        topBar = CreateBar("TopBar", 1);
        bottomBar = CreateBar("BottomBar", -1);
        CreateAreaName();
    }

    private void CreateGrayWash()
    {
        var go = new GameObject("GrayWash");
        go.transform.SetParent(overlayGo.transform, false);
        go.transform.SetAsFirstSibling(); // 置于兄弟最底层（灰罩在最底部，不能盖住黑边/名字）
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(0.4f, 0.4f, 0.42f, 0); // 灰色滤罩（alpha 0 起始，淡入后画面整体压灰）
        grayWashImage = img;
        FullScreen(img.rectTransform);
    }

    private void CreateShatter()
    {
        var go = new GameObject("Shatter");
        go.transform.SetParent(overlayGo.transform, false);
        shatterImage = go.AddComponent<Image>();
        shatterImage.raycastTarget = false;
        shatterCg = go.AddComponent<CanvasGroup>();
        shatterCg.blocksRaycasts = false;
        shatterCg.alpha = 0;
        FullScreen(shatterImage.rectTransform);
    }

    private RectTransform CreateBar(string name, int side)
    {
        var go = new GameObject(name);
        go.transform.SetParent(overlayGo.transform, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = Color.black;
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0, side == 1 ? 1 : 0);
        rt.anchorMax = new Vector2(1, side == 1 ? 1 : 0);
        rt.pivot = new Vector2(0.5f, side == 1 ? 1 : 0);
        rt.sizeDelta = new Vector2(0, barHeight);
        rt.anchoredPosition = new Vector2(0, side * barHeight);
        if (name == "TopBar") topBarImg = img;
        else if (name == "BottomBar") bottomBarImg = img;
        return rt;
    }

    private void CreateAreaName()
    {
        var go = new GameObject("AreaName");
        go.transform.SetParent(overlayGo.transform, false);
        areaName = go.AddComponent<TextMeshProUGUI>();
        areaName.font = TMP_Settings.defaultFontAsset;
        areaName.fontSize = 40f;
        areaName.fontStyle = FontStyles.Bold;
        areaName.alignment = TextAlignmentOptions.Center;
        areaName.color = Color.white;
        areaName.alpha = 0;
        areaName.raycastTarget = false;
        var rt = areaName.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(900, 80);
    }

    private void FullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void TransitionToScene(string sceneName)
    {
        if (isTransitioning || string.IsNullOrEmpty(sceneName))
            return;
        StartCoroutine(TransitionCo(sceneName));
    }

    private string GetAreaName(string sceneName)
    {
        return AreaNames.TryGetValue(sceneName, out var name) ? name : sceneName;
    }

    private IEnumerator TransitionCo(string sceneName)
    {
        isTransitioning = true;
        overlayGo.SetActive(true);
        shatterCg.blocksRaycasts = true;

        // 复位各层（过渡层跨场景复用，防止上次残留）
        topBar.anchoredPosition = new Vector2(0, barHeight);     // 上黑边移出屏外
        bottomBar.anchoredPosition = new Vector2(0, -barHeight); // 下黑边移出屏外
        if (topBarImg != null) topBarImg.color = Color.black;    // 复位黑边不透明（上次结束已淡出为透明）
        if (bottomBarImg != null) bottomBarImg.color = Color.black;
        if (grayWashImage != null)
            grayWashImage.color = new Color(grayWashImage.color.r, grayWashImage.color.g, grayWashImage.color.b, 0f); // 灰罩透明
        shatterCg.alpha = 0;                                     // 黑幕透明
        if (shatterMat != null)
        {
            shatterMat.SetFloat("_Reveal", 0f);                  // 复位中心淡出（黑幕不透明）
            shatterMat.SetFloat("_Cover", 0f);                   // 复位四周淡入（全屏透明）
        }

        if (areaName != null && areaName.font != null)
        {
            areaName.text = GetAreaName(sceneName);
            areaName.alpha = 0;
            areaName.rectTransform.anchoredPosition = new Vector2(0, 0); // 复位
        }

        // ── 0. 立即开始异步加载新场景（后台进行，不阻塞主线程 → 无卡顿）──
        var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false; // 手动控制：黑幕遮住时再激活，隐藏激活瞬间卡顿

        // ── A. 画面变灰 + 黑边同时进入 ──
        DOTween.To(() => topBar.anchoredPosition.y, v => topBar.anchoredPosition = new Vector2(0, v), 0f, grayBarDuration).SetEase(Ease.OutCubic).SetUpdate(true);
        DOTween.To(() => bottomBar.anchoredPosition.y, v => bottomBar.anchoredPosition = new Vector2(0, v), 0f, grayBarDuration).SetEase(Ease.OutCubic).SetUpdate(true);
        if (grayWashImage != null)
            grayWashImage.DOFade(0.92f, grayBarDuration).SetEase(Ease.OutCubic).SetUpdate(true); // 画面整体压灰
        yield return new WaitForSecondsRealtime(grayBarDuration);

        // ── B. 名字浮出（之后一直存在）──
        if (areaName != null && areaName.font != null)
        {
            areaName.DOFade(1f, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
            areaName.rectTransform.DOAnchorPosY(24f, 0.7f).SetEase(Ease.OutCubic).SetUpdate(true); // 轻微上浮更电影感
        }
        yield return new WaitForSecondsRealtime(nameInDuration);

        // ── C. 画面变黑（名字依旧存在）：黑幕从四周向中心反向淡入，颜色由灰渐变到黑，平滑过渡 ──
        if (shatterImage != null)
        {
            Color baseGray = grayWashImage != null
                ? new Color(grayWashImage.color.r, grayWashImage.color.g, grayWashImage.color.b, 1f)
                : new Color(0.4f, 0.4f, 0.42f, 1f); // 灰罩色兜底
            shatterImage.color = baseGray; // 黑幕初始色 = 灰罩色（四周淡入时与灰罩融合，视觉无跳变）
        }
        if (shatterMat != null)
        {
            shatterCg.alpha = 1f; // shader 在 _Cover=0 时全屏透明，直接激活，由 shader 控制四周→中心的形状
            DOTween.To(() => shatterMat.GetFloat("_Cover"), v => shatterMat.SetFloat("_Cover", v), 1f, blackCoverDuration).SetEase(Ease.InOutCubic).SetUpdate(true); // 黑幕从四周向中心淡入
        }
        else
        {
            DOTween.To(() => shatterCg.alpha, v => shatterCg.alpha = v, 1f, blackCoverDuration).SetEase(Ease.InOutCubic).SetUpdate(true); // 兜底（无 shader）：整体淡入
        }
        if (shatterImage != null)
            shatterImage.DOColor(Color.black, blackCoverDuration).SetEase(Ease.InOutCubic).SetUpdate(true); // 黑幕颜色由灰渐变到黑
        yield return new WaitForSecondsRealtime(blackCoverDuration);

        // ── C2. 黑幕盖满：黑边 + 灰罩淡出（区域名保留，在黑幕上停留至中心淡出）──
        if (grayWashImage != null)
            grayWashImage.DOFade(0f, nameBarOutDuration).SetEase(Ease.InOutCubic).SetUpdate(true); // 灰罩在黑幕下撤掉（中心淡出时不染灰）
        if (topBarImg != null)
            topBarImg.DOFade(0f, nameBarOutDuration).SetEase(Ease.InOutCubic).SetUpdate(true); // 黑边淡出
        if (bottomBarImg != null)
            bottomBarImg.DOFade(0f, nameBarOutDuration).SetEase(Ease.InOutCubic).SetUpdate(true);
        yield return new WaitForSecondsRealtime(nameBarOutDuration);

        // ── D. 等待异步加载完成（进度≥0.9 可激活），黑幕遮住激活瞬间 ──
        while (!asyncLoad.isDone)
        {
            if (asyncLoad.progress >= 0.9f)
                break;
            yield return null;
        }
        asyncLoad.allowSceneActivation = true; // 激活新场景（旧场景卸载）
        while (!asyncLoad.isDone)
            yield return null;

        // ── 4. 等新场景就绪（玩家出现 + 定位/读档恢复）──
        yield return WaitForSceneReady(sceneName == "主菜单");

        // ── E. 中心透明淡出（黑幕从中心向外消失，不愈合；名字随中心淡出一同淡化，黑边此时已淡出）──
        if (areaName != null && areaName.font != null)
            areaName.DOFade(0f, revealDuration).SetEase(Ease.InOutCubic).SetUpdate(true); // 区域名与中心淡出同步淡化
        if (shatterMat != null)
            DOTween.To(() => shatterMat.GetFloat("_Reveal"), v => shatterMat.SetFloat("_Reveal", v), 1f, revealDuration).SetEase(Ease.InOutCubic).SetUpdate(true);
        else
            DOTween.To(() => shatterCg.alpha, v => shatterCg.alpha = v, 0f, revealDuration).SetUpdate(true);
        yield return new WaitForSecondsRealtime(revealDuration);

        shatterCg.blocksRaycasts = false;
        overlayGo.SetActive(false);
        isTransitioning = false;
    }

    // 等待新场景就绪：游戏场景等玩家出现后再留缓冲；菜单场景无玩家，仅缓冲
    private IEnumerator WaitForSceneReady(bool isMenuScene)
    {
        if (!isMenuScene)
        {
            float timeout = 5f;
            while (timeout > 0)
            {
                if (FindAnyObjectByType<Player>() != null)
                    break;
                yield return null;
                timeout -= Time.unscaledDeltaTime;
            }
        }
        yield return new WaitForSecondsRealtime(readyBuffer);
    }
}
