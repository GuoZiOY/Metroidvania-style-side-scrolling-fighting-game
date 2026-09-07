
using System.Collections;
using UnityEngine;

public class VFX_AutoController: MonoBehaviour
{
    private SpriteRenderer  sr;
    public Animator anim;

    [SerializeField] private bool autoDestroy = true;//自动销毁
    [SerializeField]private float destroyDelay = 1;//销毁延迟时间
    [Space]

    [Header("特效旋转")]
    [SerializeField] private bool randomRotation = true;//随机角度旋转
    [SerializeField] private float minRotation = 0;
    [SerializeField] private float maxRotation = 360;

    [Header("特效位置")]
    [SerializeField] private bool randomOffset = true;//随机坐标偏移
    [SerializeField] private float xMinOffset = -.3f;
    [SerializeField] private float xMaxOffset = .3f;
    [Space]
    [SerializeField] private float yMinOffset = -.3f;
    [SerializeField] private float yMaxOffset = .3f;

    [Header("特效大小")]
    [SerializeField] private bool randomScale = true;//随机大小缩放
    [SerializeField] private float minScale = 1;
    [SerializeField] private float maxScale = 2;
    [SerializeField] private float pow = 3;

    [Header("淡出特效")]
    [SerializeField] private bool canFade;
    [SerializeField] private float fadeSpeed;



    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (canFade)
            StartCoroutine(FadeCo());

        ApplyRandomOffset();
        ApplyRandomRotation();
        ApplyRandomScale();

        if (autoDestroy)
            Destroy(gameObject, destroyDelay);

        
    }



    private IEnumerator FadeCo()
    {
        Color targetColor = Color.white;

        while (targetColor.a > 0)//循环，直到透明度为0
        {
            targetColor.a = targetColor.a - (fadeSpeed * Time.deltaTime);//随时间减少透明度
            sr.color = targetColor;//应用颜色
            yield return null;//等待下一帧再继续降低透明度
        }

        sr.color = targetColor;//确定更改颜色
    }

    private void ApplyRandomOffset()
    {
        if (randomOffset == false)
            return;

        float xOffset = Random.Range(xMinOffset, xMaxOffset);//获得范围内随机x坐标
        float yOffset = Random.Range(yMinOffset, yMaxOffset);//获得范围内随机y坐标

        transform.position = transform.position + new Vector3(xOffset, yOffset);//赋予特效随机坐标
    }

    private void ApplyRandomRotation()
    {
        if (randomRotation == false)
            return;

        float zRotation = Random.Range(minRotation, maxRotation);//随机角度旋转
        transform.Rotate(0, 0, zRotation);
    }

    private void ApplyRandomScale()
    {
        if (randomScale == false)
            return;
        float t = Random.Range(0f, 1f);
        t = Mathf.Pow(t, pow);//如：3，则会有70%概率落在1——1.5之间，30概率落在1.5到2之间，越接近 2 概率越低
        float scale = minScale + t * (maxScale - minScale);
        transform.localScale *= scale;
    }
}
