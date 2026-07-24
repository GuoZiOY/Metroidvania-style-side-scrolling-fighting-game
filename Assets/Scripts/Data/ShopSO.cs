using UnityEngine;

[CreateAssetMenu(menuName = "RPG设置/商店数据", fileName = "Shop -")]
public class ShopSO : ScriptableObject
{
    [System.Serializable]
    public struct ShopItem
    {
        public ItemDataSo itemData;
        public int quantity;          // -1 = 无限, 0 = 已售罄, >0 = 库存
    }

    public string shopId;
    public string shopName;
    public ShopItem[] items;                    // 出售物品列表
    [Range(0f, 1f)] public float buyBackRate = 0.5f;  // 回收折扣
}
