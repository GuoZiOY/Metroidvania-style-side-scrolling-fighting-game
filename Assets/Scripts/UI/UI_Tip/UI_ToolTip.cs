using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UI_ToolTip : MonoBehaviour
{
    public RectTransform rect;//λ��
    [SerializeField] private Vector2 offset = new Vector2(200,20);


    protected virtual void Awake()
    {
        rect = GetComponent<RectTransform>();

    }

    public virtual void ShowToolTip(bool show,RectTransform targetRect)
    {
        if (show == false)
        {
            rect.position = new Vector2(9999, 9999);
            return;
        }
        UpdatePosition(targetRect);

    }

    protected string GetColoredText(string color, string text)
    {
        return ($"<color={color}>{text}</color>");
    }

    public void UpdatePosition(RectTransform targetRect)
    {
        // 获取Canvas及其坐标系
        var canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("Tooltip不在Canvas下"); return; }
        var canvasRect = canvas.GetComponent<RectTransform>();
        var uiCamera = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? Camera.main : null;

        // 目标位置转Canvas局部空间（失败则直接返回）
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(uiCamera, targetRect.position),
            uiCamera,
            out var targetPos)) return;

        // 尺寸计算（目标、Tooltip、Canvas）
        var targetHalf = targetRect.rect.size / 2f;
        var tooltipHalf = rect.rect.size / 2f;
        var canvasHalf = canvasRect.rect.size / 2f;

        // 计算最终位置（边界检测及切换）
        var finalPos = new Vector2(
            CalculateHorizontalAxis(targetPos.x, targetHalf.x, canvasHalf.x, tooltipHalf.x, offset.x),
            CalculateVerticalAxis(targetPos.y, targetHalf.y, canvasHalf.y, tooltipHalf.y, offset.y)
        );

        // 转为世界坐标并应用（考虑父节点层级）
        var worldPos = canvasRect.TransformPoint(finalPos);
        rect.localPosition = rect.parent
            ? rect.parent.GetComponent<RectTransform>().InverseTransformPoint(worldPos)
            : finalPos;
    }

    // 计算水平轴位置（X轴：先向右，如果超出则向左）
    private float CalculateHorizontalAxis(float targetPos, float targetHalf, float canvasHalf, float tooltipHalf, float offset)
    {
        var pos = targetPos + targetHalf + offset;
        if (pos + tooltipHalf > canvasHalf)
            pos = targetPos - targetHalf - offset;
        return Mathf.Clamp(pos, -canvasHalf + tooltipHalf, canvasHalf - tooltipHalf);
    }

    // 计算垂直轴位置（Y轴：先向下，如果超出下边界则向上）
    private float CalculateVerticalAxis(float targetPos, float targetHalf, float canvasHalf, float tooltipHalf, float offset)
    {
        var pos = targetPos - targetHalf - offset; // 先向下偏移
        if (pos - tooltipHalf < -canvasHalf) // 如果超出下边界
            pos = targetPos + targetHalf + offset; // 改为向上偏移
        return Mathf.Clamp(pos, -canvasHalf + tooltipHalf, canvasHalf - tooltipHalf);
    }
}

