using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>基于 UseAction 字节码与容器成分判定可在背包内直接服用的安全消耗品。</summary>
    internal static class ConsumableClassifier
    {
        private static readonly Dictionary<MethodInfo, bool> _methodCache = new Dictionary<MethodInfo, bool>();
        private static readonly HashSet<string> ExcludedItemIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "exposedcore", "venomgland", "neuralbooster", "bunchunk"
        };
        private static readonly HashSet<string> ToxicLiquids = new HashSet<string>(StringComparer.Ordinal)
        {
            "bleach", "mindwipe", "mercury", "fentanyl", "heroin", "oxyline", "mold", "braingrow"
        };

        private static readonly OperandType[] SingleByteOps = BuildOpTable(twoByte: false);
        private static readonly OperandType[] TwoByteOps = BuildOpTable(twoByte: true);

        internal static bool IsConsumable(Item item)
        {
            if (item == null || item.Stats == null || !item.Stats.usable) return false;
            if (item.condition <= 0f) return false;
            if (item.Stats.onlyHoldInHands || item.Stats.usableOnLimb || item.Stats.useLimbAction != null) return false;

            string id = item.id ?? "";
            if (ExcludedItemIds.Contains(id)) return false;

            var method = item.Stats.useAction?.Method;
            if (method == null) return false;

            if (!_methodCache.TryGetValue(method, out bool isSafeConsumeAction))
            {
                try { isSafeConsumeAction = AnalyzeMethod(method); }
                catch (Exception ex)
                {
                    ModLog.Warning("Classify", $"消耗品判定异常 id={id}：{ex.Message}");
                    isSafeConsumeAction = false;
                }
                _methodCache[method] = isSafeConsumeAction;
                ModLog.Debug("Classify", $"首次缓存 useAction 判定：id={id} method={method.Name} safe={isSafeConsumeAction}");
            }

            if (!isSafeConsumeAction) return false;

            if (item.TryGetComponent<WaterContainerItem>(out var wat))
            {
                if (wat.CurrentTotal <= 0f) return false;
                if (wat.stack != null)
                {
                    for (int i = 0; i < wat.stack.Count; i++)
                    {
                        var entry = wat.stack[i];
                        if (entry != null && entry.amount > 0f && ToxicLiquids.Contains(entry.liquidId))
                        {
                            ModLog.DebugThrottled("toxic", "Classify", $"拦截含危险液体的容器：id={id} liquid={entry.liquidId}");
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool AnalyzeMethod(MethodInfo method)
        {
            var body = method.GetMethodBody();
            byte[] il = body?.GetILAsByteArray();
            if (il == null || il.Length == 0) return false;

            var module = method.Module;
            bool hasConsumeCall = false;
            int i = 0;
            while (i < il.Length)
            {
                byte op = il[i++];
                OperandType operandType;
                if (op == 0xFE)
                {
                    if (i >= il.Length) break;
                    operandType = TwoByteOps[il[i++]];
                }
                else
                {
                    operandType = SingleByteOps[op];
                }

                if ((op == 0x28 || op == 0x6F) && i + 4 <= il.Length)
                {
                    int token = BitConverter.ToInt32(il, i);
                    MethodBase resolved = TryResolve(module, token);
                    if (resolved != null)
                    {
                        // 调用 Body.SlotOf 的物品会在耗尽时往槽位塞空盒，在背包内触发会误塞到槽 0
                        if (resolved.DeclaringType == typeof(Body) && resolved.Name == "SlotOf")
                            return false;
                        if (IsConsumeMethod(resolved))
                            hasConsumeCall = true;
                    }
                }

                int size = GetOperandSize(operandType, il, i);
                if (size < 0 || i + size > il.Length) break;
                i += size;
            }

            return hasConsumeCall;
        }

        private static bool IsConsumeMethod(MethodBase m)
        {
            var dt = m.DeclaringType;
            string name = m.Name;
            if (dt == typeof(Body) && (name == "Eat" || name == "Drink")) return true;
            if (dt == typeof(WaterContainerItem) && name == "Drink") return true;
            if (dt == typeof(NonDescriptCan) && name == "Eat") return true;
            return false;
        }

        private static MethodBase TryResolve(Module module, int token)
        {
            try { return module.ResolveMethod(token); }
            catch { return null; }
        }

        private static int GetOperandSize(OperandType type, byte[] il, int offset)
        {
            switch (type)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    if (offset + 4 > il.Length) return -1;
                    int count = BitConverter.ToInt32(il, offset);
                    long bytes = 4L + (long)count * 4L;
                    return count < 0 || bytes > il.Length - offset ? -1 : (int)bytes;
                default:
                    return -1;
            }
        }

        private static OperandType[] BuildOpTable(bool twoByte)
        {
            var table = new OperandType[256];
            for (int i = 0; i < table.Length; i++) table[i] = (OperandType)(-1);

            foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType != typeof(OpCode)) continue;
                var code = (OpCode)f.GetValue(null);
                ushort val = (ushort)code.Value;
                if (twoByte)
                {
                    if ((val >> 8) == 0xFE) table[val & 0xFF] = code.OperandType;
                }
                else if (val <= 0xFF)
                {
                    table[val] = code.OperandType;
                }
            }
            return table;
        }
    }
}
