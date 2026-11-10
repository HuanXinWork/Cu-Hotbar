namespace CasualtiesUnknown.Hotbar
{
    /// <summary>Harmony patch 与运行时组件共享 cfg/model/view 的静态入口。</summary>
    internal static class HotbarRuntime
    {
        internal static HotbarConfig Config;
        internal static HotbarModel Model;
        internal static HotbarView View;

        internal static bool Ready => Config != null && Model != null && View != null;

        /// <summary>日志里描述物品：优先人类可读名，缺失时退回内部 id，避免打出空白看不清是哪件东西。</summary>
        internal static string DescribeItem(Item item)
        {
            if (item == null) return "空";
            string name = item.Stats != null ? item.fullName : null;
            if (string.IsNullOrEmpty(name)) name = item.id;
            return string.IsNullOrEmpty(name) ? "未知物品" : name;
        }
    }
}
