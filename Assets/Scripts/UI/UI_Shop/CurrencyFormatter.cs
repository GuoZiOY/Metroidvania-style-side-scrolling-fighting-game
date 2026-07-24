using UnityEngine;

// 货币拆分工具。将铜币值拆分为金/银/铜三级。
// UI 显示时由各组件用 Sprite 图标自行排版。
public static class CurrencyFormatter
{
    public struct CurrencyAmount
    {
        public int gold;
        public int silver;
        public int copper;

        public bool HasGold => gold > 0;
        public bool HasSilver => silver > 0;
        public bool HasCopper => copper > 0;
        public bool IsZero => gold == 0 && silver == 0 && copper == 0;
    }

    public static CurrencyAmount Split(int copperAmount)
    {
        if (copperAmount <= 0)
            return new CurrencyAmount { gold = 0, silver = 0, copper = 0 };

        int gold = copperAmount / 10000;
        int remaining = copperAmount % 10000;
        int silver = remaining / 100;
        int copper = remaining % 100;

        return new CurrencyAmount
        {
            gold = gold,
            silver = silver,
            copper = copper
        };
    }
}
