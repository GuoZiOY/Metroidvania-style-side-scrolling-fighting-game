using UnityEngine;

public class CritParticleEffect : MonoBehaviour
{
    [Header("暴击粒子预制体")]
    [SerializeField] private GameObject coreBurstPrefab;
    [SerializeField] private GameObject outerBurstPrefab;

    public void CreateCritParticles(Vector3 position, Color _ = default)
    {
        SpawnAndPlay(coreBurstPrefab, position);
        SpawnAndPlay(outerBurstPrefab, position);
    }

    private void SpawnAndPlay(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
            return;

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Destroy(go, 1f);
            return;
        }

        float lifetime = Mathf.Max(ps.main.startLifetime.constantMax, ps.main.duration);
        ps.Play();
        Destroy(go, lifetime + 0.5f);
    }
}
