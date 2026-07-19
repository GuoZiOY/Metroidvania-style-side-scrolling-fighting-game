using System.Collections.Generic;
using UnityEngine;

public class CounterShockwaveEffect : MonoBehaviour
{
    [Header("粒子预制体")]
    [SerializeField] private GameObject contractParticlePrefab;
    [SerializeField] private GameObject expandParticlePrefab;

    [Header("环形冲击波")]
    [SerializeField] private GameObject ringShockwavePrefab;

    [Header("星光")]
    [SerializeField] private GameObject starsPrefab;  // 不参与顿帧暂停

    [Header("渲染")]
    [SerializeField] private int sortingOrder = 20;

    private List<ParticleSystem> activeParticles = new List<ParticleSystem>();

    public void Spawn(Vector3 position, bool contracting)
    {
        activeParticles.Clear();

        // 粒子爆发（参与顿帧）
        GameObject prefab = contracting ? contractParticlePrefab : expandParticlePrefab;
        if (prefab != null)
            SpawnOne(prefab, position, true);

        // 环形冲击波（参与顿帧）
        if (ringShockwavePrefab != null)
            SpawnOne(ringShockwavePrefab, position, true);

        // 星光（不参与顿帧，一开始就生成并自由运动）
        if (starsPrefab != null)
            SpawnOne(starsPrefab, position, false);
    }

    private void SpawnOne(GameObject prefab, Vector3 position, bool track = true)
    {
        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Destroy(go);
            return;
        }

        ps.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder;
        if (track)
            activeParticles.Add(ps);
        ps.Play();

        float lifetime = Mathf.Max(ps.main.startLifetime.constantMax, ps.main.duration);
        Destroy(go, lifetime + 0.5f);
    }

    public void PauseAll()
    {
        foreach (var ps in activeParticles)
        {
            if (ps != null)
                ps.Pause();
        }
    }

    public void ResumeAll()
    {
        foreach (var ps in activeParticles)
        {
            if (ps != null)
                ps.Play();
        }
    }
}
