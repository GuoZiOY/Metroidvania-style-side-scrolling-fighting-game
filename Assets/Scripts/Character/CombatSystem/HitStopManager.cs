using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    private static HitStopManager instance;

    public static HitStopManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("HitStopManager");
                instance = go.AddComponent<HitStopManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("全局顿帧设置")]
    [SerializeField] private float globalHitStopDuration = 0.1f;
    [SerializeField] private float globalHitStopTimeScale = 0.01f;

    [Header("局部顿帧设置 - 三段式")]
    [SerializeField] private float localHitStopDuration = 0.08f;
    [SerializeField] private float localHitStopTimeScale = 0.0f;

    [Header("三段式顿帧配置")]
    [SerializeField] private float freezePhaseRatio = 0.4f;
    [SerializeField] private float slowRecoveryPhaseRatio = 0.3f;
    [SerializeField] private float fastRecoveryPhaseRatio = 0.3f;
    [SerializeField] private AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isGlobalHitStopActive;
    private float globalHitStopTimer;
    private float originalTimeScale;

    private List<IHitStopable> hitStopTargets = new List<IHitStopable>();
    private Dictionary<IHitStopable, HitStopData> hitStopDataMap = new Dictionary<IHitStopable, HitStopData>();

    private class HitStopData
    {
        public float totalDuration;
        public float elapsedTime;
        public float originalSpeed;
        public HitStopPhase currentPhase;
    }

    private enum HitStopPhase
    {
        Freeze,
        SlowRecovery,
        FastRecovery
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        UpdateGlobalHitStop();
        UpdateLocalHitStop();
    }

    private void UpdateGlobalHitStop()
    {
        if (!isGlobalHitStopActive)
            return;

        globalHitStopTimer -= Time.unscaledDeltaTime;

        if (globalHitStopTimer <= 0)
        {
            EndGlobalHitStop();
        }
    }

    private void UpdateLocalHitStop()
    {
        if (hitStopDataMap.Count == 0)
            return;

        List<IHitStopable> toRemove = new List<IHitStopable>();

        foreach (var pair in hitStopDataMap)
        {
            IHitStopable target = pair.Key;
            HitStopData data = pair.Value;

            data.elapsedTime += Time.unscaledDeltaTime;

            float freezeDuration = data.totalDuration * freezePhaseRatio;
            float slowRecoveryDuration = data.totalDuration * slowRecoveryPhaseRatio;
            float fastRecoveryDuration = data.totalDuration * fastRecoveryPhaseRatio;

            if (data.elapsedTime >= data.totalDuration)
            {
                target.EndHitStop();
                toRemove.Add(target);
            }
            else
            {
                float currentSpeed = 0f;

                if (data.elapsedTime <= freezeDuration)
                {
                    data.currentPhase = HitStopPhase.Freeze;
                    currentSpeed = localHitStopTimeScale;
                }
                else if (data.elapsedTime <= freezeDuration + slowRecoveryDuration)
                {
                    data.currentPhase = HitStopPhase.SlowRecovery;
                    float phaseProgress = (data.elapsedTime - freezeDuration) / slowRecoveryDuration;
                    currentSpeed = recoveryCurve.Evaluate(phaseProgress) * 0.3f;
                }
                else
                {
                    data.currentPhase = HitStopPhase.FastRecovery;
                    float phaseProgress = (data.elapsedTime - freezeDuration - slowRecoveryDuration) / fastRecoveryDuration;
                    currentSpeed = recoveryCurve.Evaluate(phaseProgress) * 0.7f + 0.3f;
                }

                target.SetAnimationSpeed(currentSpeed);
            }
        }

        foreach (var target in toRemove)
        {
            hitStopDataMap.Remove(target);
            hitStopTargets.Remove(target);
        }
    }

    public void TriggerGlobalHitStop(float duration = -1)
    {
        if (isGlobalHitStopActive)
            return;

        float actualDuration = duration > 0 ? duration : globalHitStopDuration;

        originalTimeScale = Time.timeScale;
        Time.timeScale = globalHitStopTimeScale;

        isGlobalHitStopActive = true;
        globalHitStopTimer = actualDuration;

        StartCoroutine(ResumeAfterHitStop(actualDuration));
    }

    public void TriggerLocalHitStop(IHitStopable target, float duration = -1)
    {
        if (target == null)
            return;

        float actualDuration = duration > 0 ? duration : localHitStopDuration;

        if (!hitStopTargets.Contains(target))
        {
            hitStopTargets.Add(target);
        }

        HitStopData data = new HitStopData
        {
            totalDuration = actualDuration,
            elapsedTime = 0f,
            originalSpeed = 1f,
            currentPhase = HitStopPhase.Freeze
        };

        hitStopDataMap[target] = data;

        target.StartHitStop(actualDuration);
    }

    public void TriggerLocalHitStop(List<IHitStopable> targets, float duration = -1)
    {
        if (targets == null || targets.Count == 0)
            return;

        float actualDuration = duration > 0 ? duration : localHitStopDuration;

        foreach (var target in targets)
        {
            TriggerLocalHitStop(target, actualDuration);
        }
    }

    public void TriggerLocalHitStop(GameObject attacker, GameObject victim, float duration = -1)
    {
        List<IHitStopable> targets = new List<IHitStopable>();

        if (attacker != null)
        {
            IHitStopable attackerHitStop = attacker.GetComponent<IHitStopable>();
            if (attackerHitStop != null)
            {
                targets.Add(attackerHitStop);
            }
        }

        if (victim != null)
        {
            IHitStopable victimHitStop = victim.GetComponent<IHitStopable>();
            if (victimHitStop != null)
            {
                targets.Add(victimHitStop);
            }
        }

        if (targets.Count > 0)
        {
            TriggerLocalHitStop(targets, duration);
        }
    }

    private void EndGlobalHitStop()
    {
        Time.timeScale = originalTimeScale;
        isGlobalHitStopActive = false;
    }

    private IEnumerator ResumeAfterHitStop(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);

        if (isGlobalHitStopActive)
        {
            EndGlobalHitStop();
        }
    }

    public bool IsGlobalHitStopActive => isGlobalHitStopActive;
    public bool IsLocalHitStopActive => hitStopDataMap.Count > 0;
    public int ActiveHitStopCount => hitStopDataMap.Count;
}