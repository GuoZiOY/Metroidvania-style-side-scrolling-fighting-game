using UnityEngine;

public class UI_EntityDisplay : MonoBehaviour
{
    protected Entity entity;

    protected virtual void Awake()
    {
        entity = GetComponentInParent<Entity>();
    }

    protected virtual void OnEnable()
    {
        if (entity != null)
        {
            entity.OnFlipped += HandleFlip;
        }
    }

    protected virtual void OnDisable()
    {
        if (entity != null)
        {
            entity.OnFlipped -= HandleFlip;
        }
    }

    protected virtual void HandleFlip() => transform.rotation = Quaternion.identity;
}
