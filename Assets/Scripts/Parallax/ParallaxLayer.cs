using UnityEngine;

[System.Serializable]
public class ParallaxLayer
{
    [SerializeField] private Transform background;
    [SerializeField] private float parallaxMultiplier;
    [SerializeField] private float imageWidthOffset = 10;

    private float imageFullWidth;
    private float imageHalfWidth;

    public void CalculateImageWidth()//计算背景图像的宽度
    {
        SpriteRenderer spriteRenderer = background.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            imageFullWidth = spriteRenderer.bounds.size.x;//获取背景图像的宽度
            imageHalfWidth = imageFullWidth / 2;//获取背景图像的一半宽度
        }
        else
        {
            Debug.LogError($"在 {background.name} 或其子对象中未找到 SpriteRenderer 组件");
        }
    }

    public void Move(float distanceToMove)
    {
        background.position += Vector3.right * (distanceToMove * parallaxMultiplier);//根据移动距离和视差 multiplier 移动背景
    }

    public void LoopBackground(float cameraLefteEdge, float cameraRightEdge)//循环背景
    {
        // 如果背景图像的右边缘小于相机的左边缘，将背景向右移动一个完整宽度
        float imageRightEdge = (background.position.x + imageHalfWidth) - imageWidthOffset;
        float imageLeftEdge = (background.position.x - imageHalfWidth) + imageWidthOffset;

        if (imageRightEdge < cameraLefteEdge)
            background.position += Vector3.right * imageFullWidth;//如果背景图像的右边缘小于相机的左边缘，将背景向右移动一个完整宽度
        else if (imageLeftEdge > cameraRightEdge)//??????
            background.position += Vector3.right * -imageFullWidth;
    }
}
