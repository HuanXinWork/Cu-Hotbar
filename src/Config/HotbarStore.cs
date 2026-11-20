using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>按角色标识读写快捷栏槽位物品 ID 列表。</summary>
    internal sealed class HotbarStore
    {
        private const int MpClientKeyBase = -100;

        internal string DataRoot { get; }

        internal HotbarStore()
        {
            DataRoot = ResolveDataRoot();
            try { Directory.CreateDirectory(DataRoot); }
            catch (Exception ex) { ModLog.Warning("Store", $"创建 hotbarData 目录失败：{ex.Message}"); }
        }

        /// <summary>返回当前局存储键；未就绪返回 0。</summary>
        internal static int CurrentRunId()
        {
            try
            {
                // 联机客户端每次进图都会重随 cInfo[2]，改按角色类型固定负数键避免丢失绑定与堆积文件
                if (MultiplayerCompat.IsClient)
                {
                    var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
                    if (body == null) return 0;
                    return MpClientKeyBase - Mathf.Clamp(body.charType, 0, 99);
                }

                var view = WoundView.view;
                if (view != null && view.cInfo != null && view.cInfo.Length >= 3)
                    return view.cInfo[2];
            }
            catch (Exception ex)
            {
                ModLog.Warning("Store", $"读取 runId 失败：{ex.Message}");
            }
            return 0;
        }

        internal List<string> Load(int runId)
        {
            if (runId == 0) return new List<string>();
            string path = SlotsPath(runId);
            try
            {
                if (!File.Exists(path)) return new List<string>();
                var ids = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path));
                return ids ?? new List<string>();
            }
            catch (Exception ex)
            {
                ModLog.Warning("Store", $"加载快捷栏配置失败 runId={runId}：{ex.Message}");
                return new List<string>();
            }
        }

        internal void Save(int runId, List<string> slotItemIds)
        {
            if (runId == 0) return;
            string path = SlotsPath(runId);
            string tmpPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(DataRoot);
                // net472 File.Move 无覆盖重载，先写同目录临时文件再 Copy 覆盖，防止异常中断损坏既有绑定文件
                File.WriteAllText(tmpPath, JsonConvert.SerializeObject(slotItemIds ?? new List<string>()));
                File.Copy(tmpPath, path, overwrite: true);
                File.Delete(tmpPath);
                ModLog.Debug("Store", $"已保存槽位绑定 runId={runId} -> {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                ModLog.Warning("Store", $"保存快捷栏配置失败 runId={runId}：{ex.Message}");
            }
        }

        private string SlotsPath(int runId)
        {
            string fileName = runId <= MpClientKeyBase
                ? $"mp_client_{MpClientKeyBase - runId}.json"
                : $"{runId}.json";
            return Path.Combine(DataRoot, fileName);
        }

        internal static string ResolveDataRoot()
        {
            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(asmDir)) asmDir = Application.persistentDataPath;
            return Path.Combine(asmDir, "hotbarData");
        }
    }
}
