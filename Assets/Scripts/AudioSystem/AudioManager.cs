using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class BgmGroup
    {
        public AudioClip bgmClip;
        [Range(0, 1)] public float bgmVolume = 0.5f;
    }

    [System.Serializable]
    public class EnvironmentGroup
    {
        public AudioClip chestSfxClip;
        [Range(0, 1)] public float chestVolume = 1f;
    }

    [System.Serializable]
    public class UiGroup
    {
        public AudioClip buttonSfxClip;
        [Range(0, 1)] public float buttonVolume = 1f;
        public AudioClip denySfxClip;
        [Range(0, 1)] public float denyVolume = 1f;
    }

    [System.Serializable]
    public class PlayerGroup
    {
        public AudioClip footstepSfxClip;
        [Range(0, 1)] public float footstepVolume = 1f;
        public AudioClip[] jumpSfxClips;
        [Range(0, 1)] public float jumpVolume = 1f;
        public AudioClip[] landingSfxClips;
        [Range(0, 1)] public float landingVolume = 1f;
        public AudioClip jumpAttackExtraSfxClip;
        [Range(0, 1)] public float jumpAttackExtraVolume = 1f;
    }

    [System.Serializable]
    public class CombatGroup
    {
        public AudioClip[] swingSfxClips;
        [Range(0, 1)] public float swingVolume = 1f;
        public AudioClip[] hitSfxClips;
        [Range(0, 1)] public float hitVolume = 1f;
        public AudioClip[] extraSfxClips;
        [Range(0, 1)] public float extraVolume = 1f;
        public AudioClip[] critSfxClips;
        [Range(0, 1)] public float critVolume = 1f;
        public AudioClip[] counterSuccessSfxClips;
        [Range(0, 1)] public float counterSuccessVolume = 1f;
        public AudioClip counterHitSfxClip;
        [Range(0, 1)] public float counterHitVolume = 1f;
    }

    [System.Serializable]
    public class SaveGroup
    {
        public AudioClip saveSfxClip;
        [Range(0, 1)] public float saveVolume = 1f;
        public AudioClip loadSfxClip;
        [Range(0, 1)] public float loadVolume = 1f;
    }

    [System.Serializable]
    public class TypewriterGroup
    {
        public AudioClip typewriterSfxClip;
        [Range(0, 1)] public float typewriterVolume = 1f;
    }

    [SerializeField] private BgmGroup BGM;
    [SerializeField] private EnvironmentGroup 环境;
    [SerializeField] private UiGroup UI音效;
    [SerializeField] private PlayerGroup 角色;
    [SerializeField] private CombatGroup 战斗;

    [Header("拾取")]
    [SerializeField] private AudioClip goldPickupSfxClip;
    [SerializeField, Range(0, 1)] private float goldPickupVolume = 1f;

    [SerializeField] private SaveGroup 存档;
    [SerializeField] private TypewriterGroup 打字机;

    [Header("其他")]
    [SerializeField] private float counterHitDelay = 0.1f;

    private AudioSource bgmSource;
    private AudioSource bgmSourceB;          // 第二 BGM 源（crossfade ping-pong）
    private AudioSource activeBgmSource;     // 当前实际播放的 BGM 源
    private readonly Stack<AudioClip> bgmStack = new(); // BGM 上下文栈（push/pop 恢复，防与区域音乐冲突）
    private AudioSource chestSource;
    private AudioSource uiSource;
    private AudioSource hitSource;
    private AudioSource extraSource;
    private AudioSource critSource;
    private AudioSource typewriterSource;

    private float masterVolume = 1f;
    private float sfxVolume = 1f;

    private const string MasterVolumePrefs = "Audio_MasterVolume";
    private const string BgmVolumePrefs = "Audio_BgmVolume";
    private const string SfxVolumePrefs = "Audio_SfxVolume";

    // ==================== 初始化 ====================

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolumes();
        CreateSources();
        PlayBgm();
        activeBgmSource = bgmSource;
    }

    private void CreateSources()
    {
        bgmSource = AddSource();
        bgmSourceB = AddSource();
        chestSource = AddSource();
        uiSource = AddSource();
        hitSource = AddSource();
        extraSource = AddSource();
        critSource = AddSource();
        typewriterSource = AddSource();
    }

    private AudioSource AddSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        return s;
    }

    private void LoadVolumes()
    {
        masterVolume = PlayerPrefs.GetInt(MasterVolumePrefs, 10) / 10f;
        BGM.bgmVolume = PlayerPrefs.GetInt(BgmVolumePrefs, 5) / 10f;
        sfxVolume = PlayerPrefs.GetInt(SfxVolumePrefs, 10) / 10f;
    }

    // ==================== 音量辅助 ====================

    private float Vol(float groupVolume) => sfxVolume * masterVolume * groupVolume;

    private void PlayClip(AudioSource source, AudioClip clip, float volume)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip, Vol(volume));
    }

    private AudioClip RandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    private AudioClip SafeClip(AudioClip[] clips, int index)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Mathf.Min(index, clips.Length - 1)];
    }

    // ==================== BGM ====================

    private void PlayBgm()
    {
        if (BGM.bgmClip == null || bgmSource == null) return;
        bgmSource.clip = BGM.bgmClip;
        bgmSource.loop = true;
        bgmSource.volume = masterVolume * BGM.bgmVolume;
        bgmSource.Play();
    }

    public void SetMasterVolume(int value) { masterVolume = value / 10f; PlayerPrefs.SetInt(MasterVolumePrefs, value); UpdateBgmVolume(); }
    public int GetMasterVolume() => Mathf.RoundToInt(masterVolume * 10);
    public void SetBgmVolume(int value) { BGM.bgmVolume = value / 10f; PlayerPrefs.SetInt(BgmVolumePrefs, value); UpdateBgmVolume(); }
    public int GetBgmVolume() => Mathf.RoundToInt(BGM.bgmVolume * 10);
    public void SetSfxVolume(int value) { sfxVolume = value / 10f; PlayerPrefs.SetInt(SfxVolumePrefs, value); }
    public int GetSfxVolume() => Mathf.RoundToInt(sfxVolume * 10);

    private void UpdateBgmVolume()
    {
        if (activeBgmSource != null) activeBgmSource.volume = masterVolume * BGM.bgmVolume;
    }

    // 当前正在播放的 BGM（BossEncounter 缓存/断言用）
    public AudioClip GetCurrentBgmClip() => activeBgmSource != null ? activeBgmSource.clip : BGM.bgmClip;

    // 推入 Boss 曲（缓存上一曲到栈，供 PopBgm 恢复）
    public void PushBgm(AudioClip clip, float crossfade = 1f)
    {
        if (clip == null) return;
        AudioClip prev = GetCurrentBgmClip();
        if (prev != null && prev != clip)
            bgmStack.Push(prev);
        StartCoroutine(CrossfadeTo(clip, crossfade));
    }

    // 弹出恢复上一曲（Boss 战结束/玩家死亡）
    public void PopBgm(float crossfade = 1f)
    {
        if (bgmStack.Count == 0) return;
        StartCoroutine(CrossfadeTo(bgmStack.Pop(), crossfade));
    }

    // 双源交叉淡入：新曲强拍落在旧曲淡出的同帧（落地冲击作遮罩）
    private IEnumerator CrossfadeTo(AudioClip clip, float crossfade)
    {
        AudioSource oldSource = activeBgmSource;
        AudioSource newSource = (oldSource == bgmSource) ? bgmSourceB : bgmSource;

        newSource.clip = clip;
        newSource.loop = true;
        newSource.volume = 0f;
        newSource.timeSamples = 0;
        newSource.Play();

        float t = 0f;
        while (t < crossfade)
        {
            t += Time.unscaledDeltaTime; // 音频不受 timeScale 影响，渐变用真实时间
            float k = Mathf.Clamp01(t / crossfade);
            if (oldSource != null) oldSource.volume = masterVolume * BGM.bgmVolume * (1f - k);
            newSource.volume = masterVolume * BGM.bgmVolume * k;
            yield return null;
        }
        if (oldSource != null) oldSource.Stop();
        activeBgmSource = newSource;
    }

    private void Start()
    {
        HookSceneButtons();
        SceneManager.sceneLoaded += (_, _) => HookSceneButtons();
    }

    private void HookSceneButtons()
    {
        foreach (var btn in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (btn == null || btn.gameObject.scene.name == null) continue;
            RegisterButton(btn);
        }
    }

    // ==================== UI ====================

    public void RegisterButton(Button btn)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(PlayButtonSfx);
        btn.onClick.AddListener(PlayButtonSfx);
    }

    public void PlayButtonSfx() => PlayClip(uiSource, UI音效.buttonSfxClip, UI音效.buttonVolume);
    public void PlayDenySfx() => PlayClip(uiSource, UI音效.denySfxClip, UI音效.denyVolume);

    // ==================== 环境 ====================

    public void PlayChestSfx() => PlayClip(chestSource, 环境.chestSfxClip, 环境.chestVolume);

    // ==================== 角色 ====================

    public void PlayFootstepSfx() => PlayClip(uiSource, 角色.footstepSfxClip, 角色.footstepVolume);

    public void PlayJumpSfx(bool isDoubleJump = false)
    {
        int i = isDoubleJump ? Mathf.Min(1, 角色.jumpSfxClips.Length - 1) : 0;
        PlayClip(uiSource, SafeClip(角色.jumpSfxClips, i), 角色.jumpVolume);
    }

    public void PlayLandingSfx(bool isDoubleJumpLanding = false)
    {
        int i = isDoubleJumpLanding ? Mathf.Min(1, 角色.landingSfxClips.Length - 1) : 0;
        PlayClip(uiSource, SafeClip(角色.landingSfxClips, i), 角色.landingVolume);
    }

    public void PlayJumpAttackExtraSfx() => PlayClip(hitSource, 角色.jumpAttackExtraSfxClip, 角色.jumpAttackExtraVolume);

    // ==================== 战斗 ====================

    public void PlayCounterSuccessSfx() => PlayClip(extraSource, RandomClip(战斗.counterSuccessSfxClips), 战斗.counterSuccessVolume);
    public void PlayCounterHitSfx() => StartCoroutine(DelayedCounterHit());
    public void PlaySwingSfx(int i) => PlayClip(hitSource, SafeClip(战斗.swingSfxClips, i), 战斗.swingVolume);
    public void PlayHitSfx(int i) => PlayClip(hitSource, SafeClip(战斗.hitSfxClips, i), 战斗.hitVolume);
    public void PlayExtraSfx(int i) => PlayClip(extraSource, SafeClip(战斗.extraSfxClips, i), 战斗.extraVolume);
    public void PlayCritSfx(int i) => PlayClip(critSource, SafeClip(战斗.critSfxClips, i), 战斗.critVolume);

    private IEnumerator DelayedCounterHit()
    {
        yield return new WaitForSeconds(counterHitDelay);
        PlayClip(hitSource, 战斗.counterHitSfxClip, 战斗.counterHitVolume);
    }

    // ==================== 存档 ====================

    public void PlayGoldPickupSfx() => PlayClip(uiSource, goldPickupSfxClip, goldPickupVolume);
    public void PlaySaveSfx() => PlayClip(uiSource, 存档.saveSfxClip, 存档.saveVolume);
    public void PlayLoadSfx() => PlayClip(uiSource, 存档.loadSfxClip, 存档.loadVolume);

    // ==================== 打字机 ====================

    public void PlayTypewriterSfx()
    {
        if (打字机.typewriterSfxClip == null || typewriterSource == null) return;
        typewriterSource.clip = 打字机.typewriterSfxClip;
        typewriterSource.volume = Vol(打字机.typewriterVolume);
        float start = Random.Range(0f, Mathf.Max(0f, 打字机.typewriterSfxClip.length - 0.5f));
        typewriterSource.timeSamples = Mathf.RoundToInt(start * 打字机.typewriterSfxClip.frequency);
        typewriterSource.loop = true;
        typewriterSource.Play();
    }

    public void StopTypewriterSfx()
    {
        if (typewriterSource != null && typewriterSource.isPlaying)
            typewriterSource.Stop();
    }
}
