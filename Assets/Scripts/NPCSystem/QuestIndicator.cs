using DG.Tweening;
using UnityEngine;

// NPC 头顶任务标识。
// ! 金黄感叹号 = 有可接取的任务
// ? 蓝色问号   = 有待提交/领奖的任务
public class QuestIndicator : MonoBehaviour
{
    [SerializeField] private GameObject exclamationMark;
    [SerializeField] private GameObject questionMark;
    [SerializeField] private float floatHeight = 0.3f;
    [SerializeField] private float floatSpeed = 2f;

    private NPCBehaviour npcBehaviour;
    private bool wasActive;
    private Tween floatTween;

    private void Awake()
    {
        npcBehaviour = GetComponent<NPCBehaviour>();
        if (exclamationMark != null) exclamationMark.SetActive(false);
        if (questionMark != null) questionMark.SetActive(false);
    }

    private void Update()
    {
        if (npcBehaviour == null) return;

        bool show = false;

        if (npcBehaviour.HasFinalRewardToClaim() || npcBehaviour.HasStageToSubmit())
        {
            SetIndicator(questionMark, true);
            SetIndicator(exclamationMark, false);
            show = true;
        }
        else if (npcBehaviour.HasAvailableQuest())
        {
            SetIndicator(questionMark, false);
            SetIndicator(exclamationMark, true);
            show = true;
        }
        else
        {
            SetIndicator(questionMark, false);
            SetIndicator(exclamationMark, false);
            show = false;
        }

        if (show && !wasActive) StartFloat();
        else if (!show && wasActive) StopFloat();

        wasActive = show;
    }

    private void SetIndicator(GameObject obj, bool active)
    {
        if (obj != null && obj.activeSelf != active)
            obj.SetActive(active);
    }

    private void StartFloat()
    {
        StopFloat();
        var target = (exclamationMark != null && exclamationMark.activeSelf)
            ? exclamationMark.transform
            : questionMark?.transform;
        if (target == null) return;

        Vector3 basePos = target.localPosition;
        floatTween = DOTween.To(
            () => 0f,
            t => target.localPosition = basePos + Vector3.up * Mathf.Sin(t * floatSpeed) * floatHeight,
            Mathf.PI * 2f,
            Mathf.PI * 2f / floatSpeed
        ).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear);
    }

    private void StopFloat()
    {
        floatTween?.Kill();
        floatTween = null;
        if (exclamationMark != null) exclamationMark.transform.localPosition = Vector3.zero;
        if (questionMark != null) questionMark.transform.localPosition = Vector3.zero;
    }

    private void OnDestroy() => StopFloat();
}
