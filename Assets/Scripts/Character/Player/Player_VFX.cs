using System.Collections;
using UnityEngine;

public class Player_VFX : Entity_VFX
{
    [Header("残影效果")]
    [Range(.01f, .2f)]
    public float imageEchoInterval = .08f;
    [SerializeField] private GameObject imageEchoPrefab;
    private Coroutine imageEchoCo;

    [Header("反击视觉")]
    [SerializeField] private CounterShockwaveEffect shockwaveEffect;
    [SerializeField] private float spawnOffsetRange = 1f;

    [Header("跳跃攻击落地特效")]
    [SerializeField] private GameObject fallHitVFX;
    [SerializeField] private GameObject landingStarsVFX;
    [SerializeField] private float groundYOffset = -1.4f;
    [SerializeField] private float dustXOffset = 1.5f;

    public void DoCounterVisuals(Vector3 position)
    {
        Vector3 offset = Random.insideUnitSphere * spawnOffsetRange;
        offset.z = 0;
        Vector3 spawnPos = position + offset;

        if (shockwaveEffect != null)
            shockwaveEffect.Spawn(spawnPos, Random.value > 0.5f);
    }

    public void ToggleShockwavePause(bool pause)
    {
        if (shockwaveEffect == null)
            return;

        if (pause)
            shockwaveEffect.PauseAll();
        else
            shockwaveEffect.ResumeAll();
    }

    public void PlayFallHitVFX(bool addXOffset = false, float particleScale = 1f)
    {
        Vector3 groundPos = transform.position + new Vector3(0, groundYOffset, 0);

        if (fallHitVFX != null)
            Instantiate(fallHitVFX, groundPos, Quaternion.identity);

        if (landingStarsVFX != null)
        {
            Vector3 dustPos = groundPos;
            if (addXOffset)
                dustPos += new Vector3(entity.facingDir * dustXOffset, 0, 0);

            GameObject stars = Instantiate(landingStarsVFX, dustPos, Quaternion.identity);
            ParticleSystem ps = stars.GetComponent<ParticleSystem>();

            if (particleScale > 1f)
            {
                var emission = ps.emission;
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                for (int i = 0; i < bursts.Length; i++)
                {
                    var b = bursts[i];
                    float currentCount = b.count.constant;
                    b.count = new ParticleSystem.MinMaxCurve(Mathf.RoundToInt(currentCount * particleScale));
                    bursts[i] = b;
                }
                emission.SetBursts(bursts);
            }

            ps.time = 0;
            var main = ps.main;
            main.startColor = new Color(0.55f, 0.5f, 0.45f, 0.8f);
            ps.Play();
        }
    }

    public void DoImageEchoEffect(float duration)
    {
        if (imageEchoCo != null)
            StopCoroutine(imageEchoCo);

        imageEchoCo = StartCoroutine(ImageEchoEffectCo(duration));
    }

    private IEnumerator ImageEchoEffectCo(float duration)
    {
        float timeTracker = 0;

        while (timeTracker < duration)
        {
            CreateImageEcho();

            yield return new WaitForSeconds(imageEchoInterval);
            timeTracker = timeTracker + imageEchoInterval;
        }
    }

    private void CreateImageEcho()
    {
        GameObject imageEcho = Instantiate(imageEchoPrefab, transform.position, transform.rotation);
        imageEcho.GetComponentInChildren<SpriteRenderer>().sprite = sr.sprite;
    }
}
