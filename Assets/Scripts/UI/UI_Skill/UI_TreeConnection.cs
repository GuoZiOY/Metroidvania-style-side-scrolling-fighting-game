using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class UI_TreeConnection : MonoBehaviour//技能树连接线管理器脚本
{
    [SerializeField] private RectTransform rotationPoint;//链接点
    [SerializeField] private RectTransform connectionLength;//链接线
    [SerializeField] private RectTransform childNodeConnectionPoint;//子点

    public void DirectConnection(NodeDirectionType direction,float length,float offest)//方向、长度
    {
        bool shouldBeActive = direction != NodeDirectionType.None;//如果方向类型为无，则节点不连接任何对象
        float finalLength = shouldBeActive ? length : 0;//如果激活则，最终长度赋值，否则为0
        float angle = GetDirectionAngle(direction);//获取方向角度

        rotationPoint.localRotation = Quaternion.Euler(0, 0, angle + offest);//定点旋转
        connectionLength.sizeDelta = new Vector2(finalLength, connectionLength.sizeDelta.y);//长度获取与赋值运用
    }

    public Image GetConnectionImage() => connectionLength.GetComponent<Image>();//获得连接线的组件“图像”

    public Vector2 GetConnectionPoint(RectTransform rect)//获取子节点的本地坐标
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle//RectTransform转本地坐标
            (
                rect.parent as RectTransform,
                childNodeConnectionPoint.position,//子点位置
                null,
                out var localPosition//获取本地位置
            );

        return localPosition;
    }


    private float GetDirectionAngle(NodeDirectionType type)//连接角度方向获取
    {
        switch (type)
        {
            case NodeDirectionType.UpLeft: return 135f;
            case NodeDirectionType.Up: return 90f;
            case NodeDirectionType.UpRight: return 45f;
            case NodeDirectionType.Left: return 180f;
            case NodeDirectionType.Right: return 0f;
            case NodeDirectionType.DownLeft: return -135f;
            case NodeDirectionType.Down: return -90;
            case NodeDirectionType.DownRight: return -45f;
            default: return 0f;
        }
    }
}


public enum NodeDirectionType//枚举链接线的方向
{
    None,
    UpLeft,
    Up,
    UpRight,
    Left,
    Right,
    DownLeft,
    Down,
    DownRight
}

