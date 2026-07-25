using DG.Tweening;
using UnityEngine;

// NPC 头顶任务标识。
// ! 金黄感叹号 = 有可接取的任务
// ? 蓝色问号   = 有待提交/领奖的任务
// 无标记       = 没有任务交互
public class QuestIndicator : MonoBehaviour
{
    [SerializeField] private GameObject exclamationMark;   // !
    [SerializeField] private GameObject questionMark;      // ?
    [SerializeField] private float floatHeight = 0.3f;
    [SerializeField] private float floatSpeed = 2f;

    private NPCQuestGiver giver;
    private bool wasActive;
    private Tween floatTween;

    private void Awake()
    {
        giver = GetComponent<NPCQuestGiver>();
        if (exclamationMark != null) exclamationMark.SetActive(false);
        if (questionMark != null) questionMark.SetActive(false);
    }

    private void Update()
    {
        if (giver == null) return;

        bool show = false;

        if (giver.HasFinalRewardToClaim() || giver.HasStageToSubmit())
        {
            SetIndicator(questionMark, true);
            SetIndicator(exclamationMark, false);
            show = true;
        }
        else if (giver.HasAvailableQuest())
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

        if (show && !wasActive)
            StartFloat();
        else if (!show && wasActive)
            StopFloat();

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
        if (exclamationMark != null && exclamationMark.activeSelf)
            floatTween = FloatAnimation(exclamationMark.transform);
        else if (questionMark != null && questionMark.activeSelf)
            floatTween = FloatAnimation(questionMark.transform);
    }

    private Tween FloatAnimation(Transform target)
    {
        if (target == null) return null;
        Vector3 basePos = target.localPosition;
        return DOTween.To(
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
        if (exclamationMark != null)
            exclamationMark.transform.localPosition = Vector3.zero;
        if (questionMark != null)
            questionMark.transform.localPosition = Vector3.zero;
    }

    private void OnDestroy()
    {
        StopFloat();
    }
}
