using System.Collections;
using UnityEngine;
using static Entity_Stats;

public class CritParticleEffect : MonoBehaviour
{
    [Header("暴击粒子效果")]
    [SerializeField] private GameObject particlePrefab;
    
    [Header("核心粒子")]
    [SerializeField] private ParticleConfig coreParticles = new ParticleConfig
    {
        count = 15,
        burstForce = 10f,
        size = 0.7f,
        duration = 0.8f,
        gravityScale = 0.3f,
        drag = 1.5f,
        alpha = 0.8f,
        sortingOrder = 12
    };
    
    [Header("外部粒子")]
    [SerializeField] private ParticleConfig outerParticles = new ParticleConfig
    {
        count = 25,
        burstForce = 5f,
        size = 0.3f,
        duration = 0.96f,
        gravityScale = 0.6f,
        drag = 2f,
        alpha = 0.7f,
        sortingOrder = 11
    };
    
    [Header("冲击波")]
    [SerializeField] private ShockwaveConfig shockwave = new ShockwaveConfig
    {
        enabled = true,
        duration = 0.3f,
        maxScale = 3f,
        sortingOrder = 15
    };
    
    [Header("闪光")]
    [SerializeField] private FlashConfig flash = new FlashConfig
    {
        enabled = true,
        duration = 0.15f,
        intensity = 2f,
        sortingOrder = 20
    };

    public void CreateCritParticles(Vector3 position, Color color)
    {
        if (flash.enabled)
            CreateEffect(position, color, EffectType.Flash);
        
        if (shockwave.enabled)
            CreateEffect(position, color, EffectType.Shockwave);
        
        CreateParticles(position, color, coreParticles, ParticleType.Core);
        CreateParticles(position, color, outerParticles, ParticleType.Outer);
    }

    private void CreateParticles(Vector3 position, Color color, ParticleConfig config, ParticleType type)
    {
        for (int i = 0; i < config.count; i++)
        {
            GameObject particle = CreateParticleObject(position, type);
            SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
            
            if (sr != null)
            {
                sr.color = new Color(color.r, color.g, color.b, config.alpha);
                sr.sortingOrder = config.sortingOrder;
            }
            
            SetupParticlePhysics(particle, config);
            StartCoroutine(AnimateParticle(particle, config));
        }
    }

    private GameObject CreateParticleObject(Vector3 position, ParticleType type)
    {
        if (particlePrefab != null)
        {
            return Instantiate(particlePrefab, position, Quaternion.identity);
        }
        
        GameObject particle = new GameObject(type.ToString() + "Particle");
        particle.transform.position = position;
        
        SpriteRenderer sr = particle.AddComponent<SpriteRenderer>();
        sr.sprite = GetParticleSprite(type);
        
        return particle;
    }

    private void SetupParticlePhysics(GameObject particle, ParticleConfig config)
    {
        Rigidbody2D rb = particle.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = particle.AddComponent<Rigidbody2D>();
            rb.gravityScale = config.gravityScale;
        }
        
        float angle = Random.Range(0f, 360f);
        float force = Random.Range(config.burstForce * 0.7f, config.burstForce);
        Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);
        
        rb.velocity = direction * force;
        rb.drag = config.drag;
    }

    private void CreateEffect(Vector3 position, Color color, EffectType type)
    {
        GameObject effect = new GameObject("Crit" + type);
        effect.transform.position = position;
        
        SpriteRenderer sr = effect.AddComponent<SpriteRenderer>();
        sr.sprite = GetEffectSprite(type);
        sr.color = new Color(color.r, color.g, color.b, 1f);
        sr.sortingOrder = type == EffectType.Flash ? flash.sortingOrder : shockwave.sortingOrder;
        
        StartCoroutine(AnimateEffect(effect, type));
    }

    private Sprite GetParticleSprite(ParticleType type)
    {
        int size = 32;
        int radius = 14;
        return CreateCircleSprite(size, radius);
    }

    private Sprite GetEffectSprite(EffectType type)
    {
        return type == EffectType.Flash ? CreateFlashSprite() : CreateShockwaveSprite();
    }

    private Sprite CreateCircleSprite(int size, int radius)
    {
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        
        int center = size / 2;
        for (int i = 0; i < colors.Length; i++)
        {
            int x = i % size;
            int y = i / size;
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            colors[i] = dist < radius ? Color.white : new Color(1, 1, 1, 0);
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateFlashSprite()
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];
        
        for (int i = 0; i < colors.Length; i++)
        {
            int x = i % 64;
            int y = i / 64;
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist / 32f), 2f);
            colors[i] = new Color(1, 1, 1, alpha);
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateShockwaveSprite()
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];
        
        for (int i = 0; i < colors.Length; i++)
        {
            int x = i % 64;
            int y = i / 64;
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
            colors[i] = (dist > 28 && dist < 32) ? 
                new Color(1, 1, 1, 1f - Mathf.Abs(dist - 30f) / 2f) : 
                new Color(1, 1, 1, 0);
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
    }

    private IEnumerator AnimateParticle(GameObject particle, ParticleConfig config)
    {
        SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Destroy(particle);
            yield break;
        }
        
        float timer = 0f;
        particle.transform.localScale = Vector3.one * config.size;
        Vector3 startScale = particle.transform.localScale;
        
        while (timer < config.duration)
        {
            timer += Time.deltaTime;
            float progress = timer / config.duration;
            
            particle.transform.localScale = startScale * (1f - progress * 0.5f);
            yield return null;
        }
        
        if (particle != null)
            Destroy(particle);
    }

    private IEnumerator AnimateEffect(GameObject effect, EffectType type)
    {
        SpriteRenderer sr = effect.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Destroy(effect);
            yield break;
        }
        
        float duration = type == EffectType.Flash ? flash.duration : shockwave.duration;
        float timer = 0f;
        
        if (type == EffectType.Flash)
        {
            effect.transform.localScale = Vector3.one * 0.5f;
            Vector3 startScale = effect.transform.localScale;
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                effect.transform.localScale = startScale * (1f + progress * 2f);
                yield return null;
            }
        }
        else
        {
            effect.transform.localScale = Vector3.one * 0.1f;
            
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                
                effect.transform.localScale = Vector3.one * (0.1f + progress * shockwave.maxScale);
                yield return null;
            }
        }
        
        if (effect != null)
            Destroy(effect);
    }

    [System.Serializable]
    private class ParticleConfig
    {
        public int count;
        public float burstForce;
        public float size;
        public float duration;
        public float gravityScale;
        public float drag;
        public float alpha;
        public int sortingOrder;
    }

    [System.Serializable]
    private class ShockwaveConfig
    {
        public bool enabled;
        public float duration;
        public float maxScale;
        public int sortingOrder;
    }

    [System.Serializable]
    private class FlashConfig
    {
        public bool enabled;
        public float duration;
        public float intensity;
        public int sortingOrder;
    }

    private enum ParticleType
    {
        Core,
        Outer
    }

    private enum EffectType
    {
        Flash,
        Shockwave
    }
}
