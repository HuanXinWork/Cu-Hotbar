using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>分级日志系统：Release 仅输出精简运行与告警日志，Debug 启用全链路追踪与独立诊断日志文件。</summary>
    internal static class ModLog
    {
        private const string Prefix = "[Hotbar] ";

#if DEBUG
        internal const string BuildFlavor = "Debug";
#else
        internal const string BuildFlavor = "Release";
#endif

        private static ManualLogSource _sink;
        private static MethodInfo _consoleLog;
        private static bool _consoleResolved;

#if DEBUG
        private static readonly object _fileLock = new object();
        private static readonly Dictionary<string, ThrottleState> _throttle
            = new Dictionary<string, ThrottleState>(StringComparer.Ordinal);
        private static StreamWriter _debugWriter;
        private static string _debugFilePath;
#endif

        internal static bool ShowInConsole { get; set; }

        internal static string FormatDisplayVersion(string version)
        {
#if DEBUG
            return version + " (Debug)";
#else
            return version;
#endif
        }

        internal static void Init(ManualLogSource sink, string dataRoot)
        {
            _sink = sink;
#if DEBUG
            InitDebugFile(dataRoot);
#endif
        }

        internal static void Shutdown()
        {
#if DEBUG
            lock (_fileLock)
            {
                if (_debugWriter != null)
                {
                    try
                    {
                        _debugWriter.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}][END] Shutdown");
                        _debugWriter.Flush();
                        _debugWriter.Dispose();
                    }
                    catch { }
                    _debugWriter = null;
                }
            }
#endif
        }

        internal static void Info(string message) => Info(null, message);

        internal static void Info(string scope, string message)
        {
            string formatted = FormatScoped(scope, message);
            _sink?.LogInfo(formatted);
            WriteToConsole(formatted, null);
#if DEBUG
            WriteDebugFile("INF", scope, message);
#endif
        }

        internal static void Warning(string message) => Warning(null, message);

        internal static void Warning(string scope, string message)
        {
            string formatted = FormatScoped(scope, message);
            _sink?.LogWarning(formatted);
            WriteToConsole(formatted, "yellow");
#if DEBUG
            WriteDebugFile("WRN", scope, message);
#endif
        }

        internal static void Error(string message) => Error(null, message);

        internal static void Error(string scope, string message)
        {
            string formatted = FormatScoped(scope, message);
            _sink?.LogError(formatted);
            WriteToConsole(formatted, "red");
#if DEBUG
            WriteDebugFile("ERR", scope, message);
#endif
        }

        /// <summary>Debug 专用状态日志；Release 编译期连同实参求值一并剥离。</summary>
        [Conditional("DEBUG")]
        internal static void Debug(string scope, string message)
        {
#if DEBUG
            string formatted = $"[DBG][{scope}] {message}";
            // BepInEx 默认屏蔽 LogDebug 级别，走 LogInfo 确保写入 LogOutput.log
            _sink?.LogInfo(Prefix + formatted);
            WriteToConsole(formatted, "#9E9E9E");
            WriteDebugFile("DBG", scope, message);
#endif
        }

        /// <summary>Debug 专用多步流程追踪日志。</summary>
        [Conditional("DEBUG")]
        internal static void Flow(string scope, string step, string message)
        {
#if DEBUG
            Debug(scope, $"{step} · {message}");
#endif
        }

        /// <summary>Debug 专用按分类限频（1 次/秒）日志，附带上一秒合并计数。</summary>
        [Conditional("DEBUG")]
        internal static void DebugThrottled(string category, string scope, string message)
        {
#if DEBUG
            long second = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            string suffix;
            lock (_fileLock)
            {
                if (!_throttle.TryGetValue(category, out var state))
                {
                    state = new ThrottleState { Second = second };
                    _throttle[category] = state;
                    suffix = "";
                }
                else if (state.Second == second)
                {
                    state.Merged++;
                    return;
                }
                else
                {
                    suffix = state.Merged > 0 ? $"（上一秒合并 {state.Merged} 次）" : "";
                    state.Second = second;
                    state.Merged = 0;
                }
            }
            Debug(scope, message + suffix);
#endif
        }

        private static string FormatScoped(string scope, string message)
        {
            return string.IsNullOrEmpty(scope) ? message : $"[{scope}] {message}";
        }

#if DEBUG
        private sealed class ThrottleState
        {
            internal long Second;
            internal int Merged;
        }

        private static void InitDebugFile(string dataRoot)
        {
            if (string.IsNullOrEmpty(dataRoot)) return;
            lock (_fileLock)
            {
                if (_debugWriter != null) return;
                try
                {
                    Directory.CreateDirectory(dataRoot);
                    _debugFilePath = Path.Combine(dataRoot, "CuHotbar.debug.log");
                    string prevPath = Path.Combine(dataRoot, "CuHotbar.debug.prev.log");
                    if (File.Exists(_debugFilePath))
                    {
                        if (File.Exists(prevPath)) File.Delete(prevPath);
                        File.Move(_debugFilePath, prevPath);
                    }
                    var stream = new FileStream(_debugFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    _debugWriter = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    _debugWriter.WriteLine($"=== {Plugin.PluginName} v{Plugin.PluginVersion} ({BuildFlavor}) · {DateTime.Now:yyyy-MM-dd HH:mm:ss} · Unity {Application.unityVersion} ===");
                }
                catch (Exception ex)
                {
                    _sink?.LogWarning($"{Prefix}创建独立诊断日志失败：{ex.Message}");
                }
            }
        }

        private static void WriteDebugFile(string level, string scope, string message)
        {
            if (_debugWriter == null) return;
            int frame = 0;
            try { frame = Time.frameCount; } catch { }
            string tag = string.IsNullOrEmpty(scope) ? "" : $"[{scope}]";
            lock (_fileLock)
            {
                try
                {
                    _debugWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}][F{frame:D5}][{level}]{tag} {message}");
                }
                catch { }
            }
        }
#endif

        private static void WriteToConsole(string message, string color)
        {
            if (!ShowInConsole) return;
            try
            {
                var instance = ConsoleScript.instance;
                if (instance == null || !ResolveConsoleLog()) return;
                string text = string.IsNullOrEmpty(color) ? Prefix + message : $"<color={color}>{Prefix}{message}</color>";
                _consoleLog.Invoke(instance, new object[] { text });
            }
            catch { }
        }

        private static bool ResolveConsoleLog()
        {
            if (_consoleResolved) return _consoleLog != null;
            _consoleResolved = true;
            _consoleLog = typeof(ConsoleScript).GetMethod("LogToConsole",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(string) }, null);
            return _consoleLog != null;
        }
    }
}
