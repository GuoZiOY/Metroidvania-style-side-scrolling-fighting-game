using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_VFX : Entity_VFX
{
    [Header("??????")]
    [Range(.01f, .2f)]
    public float imageEchoInterval = .08f;// ??????
    [SerializeField] private GameObject imageEchoPrefab;// ???????
    private Coroutine imageEchoCo;

    public void DoImageEchoEffect(float duration)// ????????
    {
        if (imageEchoCo != null)
            StopCoroutine(imageEchoCo);

        imageEchoCo = StartCoroutine(ImageEchoEffectCo(duration));
    }


    private IEnumerator ImageEchoEffectCo(float duration)// ????????
    {
        float timeTracker = 0;

        while (timeTracker < duration)
        {
            CreateImageEcho();

            yield return new WaitForSeconds(imageEchoInterval);
            timeTracker = timeTracker + imageEchoInterval;
        }
    }

    private void CreateImageEcho() // ??????
    {
        GameObject imageEcho = Instantiate(imageEchoPrefab, transform.position, transform.rotation);
        imageEcho.GetComponentInChildren<SpriteRenderer>().sprite = sr.sprite;
    }
}
