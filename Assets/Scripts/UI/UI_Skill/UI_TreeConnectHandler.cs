using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


[Serializable]
public class UI_TreeConnectDetails//技能树连接细节
{
    public UI_TreeConnectHandler childNode;
    public NodeDirectionType direction;
    [Range(100f, 350f)] public float length;
    [Range(-30f, 30f)] public float rotation;
}

[ExecuteAlways]
public class UI_TreeConnectHandler : MonoBehaviour
{
    private RectTransform rect => GetComponent<RectTransform>();
    [SerializeField] private UI_TreeConnectDetails[] connectionDetails;
    [SerializeField] private UI_TreeConnection[] connections;

    private Image connectionImage;//连接线的图像
    private Color originalColor;//连接线的图像初始颜色

    private void Awake()
    {
        if (connectionImage != null) //开始时，保存连接线的颜色
           originalColor = connectionImage.color;
    }

    public UI_TreeNode[] GetChildNodes()
    {
        List<UI_TreeNode> ChildrenToReturn = new List<UI_TreeNode>();//创建空列表——要返回的子节点

        foreach (var node in connectionDetails)//遍历连接详情中的每个线节点
        {
            if(node.childNode != null)//如果节点不为空
                ChildrenToReturn.Add(node.childNode.GetComponent<UI_TreeNode>());//添加 获取到的子节点的UI树节点组件 到列表
        }
        return ChildrenToReturn.ToArray();//转数组

    }


    private void OnValidate()
    {
        if (connectionDetails.Length <= 0)
            return;

        if (connectionDetails.Length != connections.Length)
        {
            Debug.Log("Amount of details should be same as amount of connections. - " + gameObject.name);
            return;
        }

        UpdateConnections();
    }

    public void UpdateConnections()//更新位置
    {
        for (int i = 0; i < connectionDetails.Length; i++)
        {
            var detail = connectionDetails[i];
            var connection = connections[i];

            Vector2 targetPosition = connection.GetConnectionPoint(rect);
            Image connectionImage = connection.GetConnectionImage();

            connection.DirectConnection(detail.direction, detail.length, detail.rotation);

            if (detail.childNode == null)//如果子节点为空，继续
                continue;

            detail.childNode.SetPosition(targetPosition);//位置更新
            detail.childNode.SetConnectionImage(connectionImage);//连接线的图像更新
            //detail.childNode.transform.SetAsLastSibling();
            //设为最后一个同级，成为层级内最后一个子项，避免自动整理后暴露在线和点的层级之上

        }
    }

    public void UpdateAllConnections()//整理技能树——遍历更新所有子节点的位置
    {
        UpdateConnections();

        foreach (var node in connectionDetails)
        {
            if (node.childNode == null) continue;
            node.childNode?.UpdateConnections();
        }
    }

    public void UnlockConnectionImage(bool unlocked)
    {
        if (connectionImage == null)
            return;

        connectionImage.color = unlocked ? Color.white : originalColor;
    }

    public void SetConnectionImage(Image image) => connectionImage = image;
    public void SetPosition(Vector2 position) => rect.anchoredPosition = position;
}

