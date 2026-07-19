using System.Collections;
using UnityEngine;

public class HitSlashEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.3f;
    [SerializeField] private float startScale = 0.5f;
    [SerializeField] private float endScale = 2f;

    private SpriteRenderer sr;
    private Material mat;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            mat = sr.material;
    }

    private void OnEnable()
    {
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float elapsed = 0;
        transform.localScale = Vector3.one * startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 放大
            float scale = Mathf.Lerp(startScale, endScale, t);
            transform.localScale = Vector3.one * scale;

            // 溶解进度：0 → 0.5（前半段溶解出现，后半段保持）
            if (mat != null)
            {
                float dissolve = Mathf.Lerp(0.5f, 0, t * 2f);
                mat.SetFloat("_DissolveAmount", Mathf.Clamp01(dissolve));

                // 纹理缓慢滑动
                mat.SetVector("_MainTexOffset", new Vector2(t * 0.2f, 0));
            }

            yield return null;
        }

        // 快速淡出
        if (mat != null)
        {
            float elapsed2 = 0;
            while (elapsed2 < 0.1f)
            {
                elapsed2 += Time.deltaTime;
                mat.SetFloat("_DissolveAmount", Mathf.Lerp(0, 1, elapsed2 / 0.1f));
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (mat != null)
            Destroy(mat);
    }
}
