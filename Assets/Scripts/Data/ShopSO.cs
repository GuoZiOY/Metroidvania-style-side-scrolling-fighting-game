using UnityEngine;

[CreateAssetMenu(menuName = "RPG设置/商店数据", fileName = "Shop -")]
public class ShopSO : ScriptableObject
{
    public string shopId;
    public string shopName;
    public ItemDataSo[] items;                    // 出售物品列表（读取 buyPrice > 0 的）
    [Range(0f, 1f)] public float buyBackRate = 0.5f;  // 回收折扣
}
