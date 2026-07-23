using UnityEngine;
using UnityEngine.UI;

public class UI_Setting : MonoBehaviour
{
    #region 字段和属性

    [Header("攻击特效颜色设置")]
    [SerializeField] private Toggle playerHitVFXColorToggle;
    [SerializeField] private Toggle enemyHitVFXColorToggle;
    [SerializeField] private Player player;

    [Header("音量设置")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMPro.TextMeshProUGUI masterVolumeText;
    [SerializeField] private TMPro.TextMeshProUGUI bgmVolumeText;
    [SerializeField] private TMPro.TextMeshProUGUI sfxVolumeText;

    private Entity_VFX playerVFX;
    private Enemy[] enemies;
    private Enemy_VFX[] enemyVFXs;

    #endregion

    #region Unity生命周期

    private void Awake()
    {
        InitializeReferences();
    }

    private void Start()
    {
        InitializeReferences();
        InitializeHitVFXColorToggles();
        InitializeVolumeSliders();
    }

    #endregion

    #region 初始化方法

    private void InitializeReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        enemies = FindObjectsByType<Enemy>();
        
        if (player != null)
        {
            playerVFX = player.GetComponent<Entity_VFX>();
        }

        if (enemies != null && enemies.Length > 0)
        {
            enemyVFXs = new Enemy_VFX[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                enemyVFXs[i] = enemies[i].GetComponent<Enemy_VFX>();
            }
        }
    }

    private void InitializeHitVFXColorToggles()
    {
        SetupPlayerHitVFXToggle();
        SetupEnemyHitVFXToggle();
    }

    #endregion

    #region 音量设置

    private void InitializeVolumeSliders()
    {
        if (AudioManager.Instance == null)
            return;

        SetupVolumeSlider(masterVolumeSlider, masterVolumeText, AudioManager.Instance.GetMasterVolume(), v => AudioManager.Instance.SetMasterVolume(v));
        SetupVolumeSlider(bgmVolumeSlider, bgmVolumeText, AudioManager.Instance.GetBgmVolume(), v => AudioManager.Instance.SetBgmVolume(v));
        SetupVolumeSlider(sfxVolumeSlider, sfxVolumeText, AudioManager.Instance.GetSfxVolume(), v => AudioManager.Instance.SetSfxVolume(v));
    }

    private static void SetupVolumeSlider(Slider slider, TMPro.TextMeshProUGUI label, int initValue, System.Action<int> onChanged)
    {
        if (slider == null) return;

        slider.minValue = 0;
        slider.maxValue = 10;
        slider.wholeNumbers = true;
        slider.value = initValue;

        if (label != null)
            label.text = initValue.ToString();

        slider.onValueChanged.AddListener(v =>
        {
            int val = Mathf.RoundToInt(v);
            onChanged(val);
            if (label != null)
                label.text = val.ToString();
        });
    }

    #endregion

    #region 攻击特效颜色管理

    private void SetupPlayerHitVFXToggle()
    {
        if (playerHitVFXColorToggle == null)
        {
            return;
        }

        playerHitVFXColorToggle.isOn = playerVFX != null && playerVFX.closeHitVFXColor;
        playerHitVFXColorToggle.onValueChanged.AddListener(OnPlayerHitVFXColorChanged);
    }

    private void SetupEnemyHitVFXToggle()
    {
        if (enemyHitVFXColorToggle == null)
        {
            return;
        }

        bool allCloseHitVFXColor = true;
        if (enemyVFXs != null && enemyVFXs.Length > 0)
        {
            foreach (var vfx in enemyVFXs)
            {
                if (vfx != null && !vfx.closeHitVFXColor)
                {
                    allCloseHitVFXColor = false;
                    break;
                }
            }
        }
        else
        {
            allCloseHitVFXColor = false;
        }

        enemyHitVFXColorToggle.isOn = allCloseHitVFXColor;
        enemyHitVFXColorToggle.onValueChanged.AddListener(OnEnemyHitVFXColorChanged);
    }

    private void OnPlayerHitVFXColorChanged(bool isOn)
    {
        if (playerVFX != null)
        {
            playerVFX.closeHitVFXColor = isOn;
            Debug.Log($"角色攻击特效颜色关闭: {(isOn ? "是" : "否")}");
        }
    }

    private void OnEnemyHitVFXColorChanged(bool isOn)
    {
        if (enemyVFXs != null && enemyVFXs.Length > 0)
        {
            foreach (var vfx in enemyVFXs)
            {
                if (vfx != null)
                {
                    vfx.closeHitVFXColor = isOn;
                }
            }
            Debug.Log($"敌人攻击特效颜色: {(isOn ? "开启" : "关闭")} (共 {enemyVFXs.Length} 个敌人)");
        }
    }

    #endregion
}
