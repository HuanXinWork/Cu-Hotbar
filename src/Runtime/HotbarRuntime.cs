namespace CasualtiesUnknown.Hotbar
{
    /// <summary>供 Harmony 补丁与运行时组件共享配置、模型与视图实例的静态入口。</summary>
    internal static class HotbarRuntime
    {
        internal static HotbarConfig Config;
        internal static HotbarModel Model;
        internal static HotbarView View;

        internal static bool Ready => Config != null && Model != null && View != null;

        internal static string DescribeItem(Item item)
        {
            if (item == null) return "空";
            string name = item.Stats != null ? item.fullName : null;
            if (string.IsNullOrEmpty(name)) name = item.id;
            return string.IsNullOrEmpty(name) ? "未知物品" : name;
        }
    }
}
