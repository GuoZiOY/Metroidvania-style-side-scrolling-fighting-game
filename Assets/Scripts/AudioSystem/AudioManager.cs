using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    public AudioClip bgmClip;
    [SerializeField, Range(0, 1)] private float bgmVolume = 0.5f;

    [Header("环境音效")]
    public AudioClip chestSfxClip;
    [SerializeField, Range(0, 1)] private float chestVolume = 1f;

    [Header("UI音效")]
    public AudioClip buttonSfxClip;
    [SerializeField, Range(0, 1)] private float buttonVolume = 1f;
    public AudioClip denySfxClip;
    [SerializeField, Range(0, 1)] private float denyVolume = 1f;

    [Header("角色音效")]
    public AudioClip footstepSfxClip;
    [SerializeField, Range(0, 1)] private float footstepVolume = 1f;
    public AudioClip[] jumpSfxClips;
    [SerializeField, Range(0, 1)] private float jumpVolume = 1f;
    public AudioClip[] landingSfxClips;
    [SerializeField, Range(0, 1)] private float landingVolume = 1f;
    public AudioClip jumpAttackExtraSfxClip;
    [SerializeField, Range(0, 1)] private float jumpAttackExtraVolume = 1f;

    [Header("战斗音效")]
    public AudioClip[] swingSfxClips;
    [SerializeField, Range(0, 1)] private float swingVolume = 1f;
    public AudioClip[] hitSfxClips;
    [SerializeField, Range(0, 1)] private float hitVolume = 1f;
    public AudioClip[] extraSfxClips;
    [SerializeField, Range(0, 1)] private float extraVolume = 1f;
    public AudioClip[] critSfxClips;
    [SerializeField, Range(0, 1)] private float critVolume = 1f;
    public AudioClip[] counterSuccessSfxClips;
    [SerializeField, Range(0, 1)] private float counterSuccessVolume = 1f;
    public AudioClip counterHitSfxClip;
    [SerializeField, Range(0, 1)] private float counterHitVolume = 1f;

    [Header("存档音效")]
    public AudioClip saveSfxClip;
    [SerializeField, Range(0, 1)] private float saveVolume = 1f;
    public AudioClip loadSfxClip;
    [SerializeField, Range(0, 1)] private float loadVolume = 1f;

    [Header("其他设置")]
    [SerializeField] private float fadeInDuration = 2f;
    [SerializeField] private float counterHitDelay = 0.1f;

    private AudioSource bgmSource;
    private AudioSource chestSource;
    private AudioSource uiSource;
    private AudioSource hitSource;
    private AudioSource extraSource;
    private AudioSource critSource;
    private bool isFadingIn;

    private float masterVolume = 1f;
    private float sfxVolume = 1f;

    private const string MasterVolumePrefs = "Audio_MasterVolume";
    private const string BgmVolumePrefs = "Audio_BgmVolume";
    private const string SfxVolumePrefs = "Audio_SfxVolume";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        masterVolume = PlayerPrefs.GetFloat(MasterVolumePrefs, 1f);
        bgmVolume = PlayerPrefs.GetFloat(BgmVolumePrefs, 0.5f);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumePrefs, 1f);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        chestSource = gameObject.AddComponent<AudioSource>();
        chestSource.playOnAwake = false;

        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;

        hitSource = gameObject.AddComponent<AudioSource>();
        hitSource.playOnAwake = false;

        extraSource = gameObject.AddComponent<AudioSource>();
        extraSource.playOnAwake = false;

        critSource = gameObject.AddComponent<AudioSource>();
        critSource.playOnAwake = false;
    }

    private void Start()
    {
        HookAllButtons();
        if (bgmClip != null)
            StartBgm();
        else
            Debug.LogWarning("AudioManager: bgmClip 未赋值");
    }

    private void HookAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button btn in buttons)
            btn.onClick.AddListener(PlayButtonSfx);
    }

    private void StartBgm()
    {
        bgmSource.clip = bgmClip;
        bgmSource.volume = 0;
        bgmSource.Play();
        StartCoroutine(FadeInBgm());
    }

    private IEnumerator FadeInBgm()
    {
        float targetVolume = bgmVolume * masterVolume;
        float timer = 0;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0, targetVolume, timer / fadeInDuration);
            yield return null;
        }
        bgmSource.volume = targetVolume;
        isFadingIn = false;
    }

    // ===== 环境音效 =====

    public void PlayChestSfx()
    {
        if (chestSfxClip == null) return;
        if (chestSource != null)
            chestSource.PlayOneShot(chestSfxClip, sfxVolume * masterVolume * chestVolume);
    }

    // ===== 角色音效 =====

    public void PlayFootstepSfx()
    {
        if (footstepSfxClip == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(footstepSfxClip, sfxVolume * masterVolume * footstepVolume);
    }

    public void PlayJumpSfx(bool isDoubleJump = false)
    {
        int index = isDoubleJump ? 1 : 0;
        if (jumpSfxClips == null || index >= jumpSfxClips.Length || jumpSfxClips[index] == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(jumpSfxClips[index], sfxVolume * masterVolume * jumpVolume);
    }

    public void PlayLandingSfx(bool isDoubleJumpLanding = false)
    {
        int index = isDoubleJumpLanding ? 1 : 0;
        if (landingSfxClips == null || index >= landingSfxClips.Length || landingSfxClips[index] == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(landingSfxClips[index], sfxVolume * masterVolume * landingVolume);
    }

    public void PlayJumpAttackExtraSfx()
    {
        if (jumpAttackExtraSfxClip == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(jumpAttackExtraSfxClip, sfxVolume * masterVolume * jumpAttackExtraVolume);
    }

    // ===== UI 音效 =====

    public void PlayButtonSfx()
    {
        if (buttonSfxClip == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(buttonSfxClip, sfxVolume * masterVolume * buttonVolume);
    }

    public void PlayDenySfx()
    {
        if (denySfxClip == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(denySfxClip, sfxVolume * masterVolume * denyVolume);
    }

    public void PlaySaveSfx()
    {
        if (saveSfxClip == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(saveSfxClip, sfxVolume * masterVolume * saveVolume);
    }

    public void PlayLoadSfx()
    {
        if (loadSfxClip == null) return;
        if (uiSource != null)
            uiSource.PlayOneShot(loadSfxClip, sfxVolume * masterVolume * loadVolume);
    }

    // ===== 战斗音效 =====

    public void PlayCounterSuccessSfx()
    {
        if (counterSuccessSfxClips == null || counterSuccessSfxClips.Length == 0) return;
        AudioClip clip = counterSuccessSfxClips[Random.Range(0, counterSuccessSfxClips.Length)];
        if (clip != null && extraSource != null)
            extraSource.PlayOneShot(clip, sfxVolume * masterVolume * counterSuccessVolume);
    }

    public void PlayCounterHitSfx()
    {
        if (counterHitSfxClip == null) return;
        if (hitSource != null)
            hitSource.PlayOneShot(counterHitSfxClip, sfxVolume * masterVolume * counterHitVolume);
    }

    public void PlayCounterWithDelay()
    {
        PlayCounterSuccessSfx();
        StartCoroutine(DelayedCounterSfx());
    }

    private IEnumerator DelayedCounterSfx()
    {
        yield return new WaitForSeconds(counterHitDelay);
        PlayCounterHitSfx();
    }

    public void PlaySwingSfx(int comboIndex)
    {
        if (swingSfxClips == null || swingSfxClips.Length == 0) return;
        int index = Mathf.Clamp(comboIndex - 1, 0, swingSfxClips.Length - 1);
        AudioClip clip = swingSfxClips[index];
        if (clip != null && hitSource != null)
            hitSource.PlayOneShot(clip, sfxVolume * masterVolume * swingVolume);
    }

    public void PlayHitSfx(int comboIndex)
    {
        if (hitSfxClips == null || hitSfxClips.Length == 0) return;
        int index = Mathf.Clamp(comboIndex - 1, 0, hitSfxClips.Length - 1);
        AudioClip clip = hitSfxClips[index];
        if (clip != null && hitSource != null)
            hitSource.PlayOneShot(clip, sfxVolume * masterVolume * hitVolume);
    }

    public void PlayExtraSfx(int comboIndex)
    {
        if (extraSfxClips == null || extraSfxClips.Length == 0) return;
        int index = Mathf.Clamp(comboIndex - 1, 0, extraSfxClips.Length - 1);
        AudioClip clip = extraSfxClips[index];
        if (clip != null && extraSource != null)
            extraSource.PlayOneShot(clip, sfxVolume * masterVolume * extraVolume);
    }

    public void PlayCritSfx(int comboIndex)
    {
        if (critSfxClips == null || critSfxClips.Length == 0) return;
        int index = Mathf.Clamp(comboIndex - 1, 0, critSfxClips.Length - 1);
        AudioClip clip = critSfxClips[index];
        if (clip != null && critSource != null)
            critSource.PlayOneShot(clip, sfxVolume * masterVolume * critVolume);
    }

    public void RegisterButton(Button btn)
    {
        if (btn != null)
            btn.onClick.AddListener(PlayButtonSfx);
    }

    // ===== 音量 API =====

    public int GetMasterVolume() => Mathf.RoundToInt(masterVolume * 10);
    public int GetBgmVolume() => Mathf.RoundToInt(bgmVolume * 10);
    public int GetSfxVolume() => Mathf.RoundToInt(sfxVolume * 10);

    public void SetMasterVolume(int value)
    {
        masterVolume = Mathf.Clamp(value, 0, 10) / 10f;
        PlayerPrefs.SetFloat(MasterVolumePrefs, masterVolume);
        PlayerPrefs.Save();
        if (bgmSource != null && !isFadingIn)
            bgmSource.volume = bgmVolume * masterVolume;
    }

    public void SetBgmVolume(int value)
    {
        bgmVolume = Mathf.Clamp(value, 0, 10) / 10f;
        PlayerPrefs.SetFloat(BgmVolumePrefs, bgmVolume);
        PlayerPrefs.Save();
        if (bgmSource != null && !isFadingIn)
            bgmSource.volume = bgmVolume * masterVolume;
    }

    public void SetSfxVolume(int value)
    {
        sfxVolume = Mathf.Clamp(value, 0, 10) / 10f;
        PlayerPrefs.SetFloat(SfxVolumePrefs, sfxVolume);
        PlayerPrefs.Save();
    }
}
