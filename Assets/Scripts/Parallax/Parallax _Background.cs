using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    private Camera mainCamera; // 主相机引用（用于获取相机位置和视野范围）
    private float lastCameraPositionX; // 上一帧相机的X坐标（用于计算相机移动距离）
    private float cameraHalfWidth; // 相机视野半宽（用于计算相机边缘位置）

    [SerializeField] private ParallaxLayer[] backgroundLayers;// 所有视差图层的数组（在Inspector中配置多个图层）

    private void Awake()
    {
        mainCamera = Camera.main;
        cameraHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;// 计算正交相机的视野半宽（正交相机宽度 = 正交大小 × 宽高比 × 2，半宽即除以2）
        InitializeLayers();// 初始化所有图层（计算图片宽度）
    }

    private void FixedUpdate()//固定帧更新（使背景平滑移动，不会出现像素抖动的关键之一）
    {
        float currentCameraPositionX = mainCamera.transform.position.x;// 获取当前相机X坐标
        float distanceToMove = currentCameraPositionX - lastCameraPositionX;// 计算相机在X轴上的移动距离（当前位置 - 上一帧位置）
        lastCameraPositionX = currentCameraPositionX;// 更新上一帧相机位置（用于下一帧计算）
        // 计算当前相机的左边缘和右边缘X坐标
        float cameraLeftEdge = currentCameraPositionX - cameraHalfWidth;
        float cameraRightEdge = currentCameraPositionX + cameraHalfWidth;

        foreach (ParallaxLayer layer in backgroundLayers)
        {
            layer.Move(distanceToMove);// 按相机移动距离移动图层
            layer.LoopBackground(cameraLeftEdge, cameraRightEdge);// 处理图层无缝循环
        }
    }

    private void InitializeLayers()// 遍历数组调用每个图层的宽度计算方法
    {
        foreach (ParallaxLayer layer in backgroundLayers)
            layer.CalculateImageWidth();
    }
}
