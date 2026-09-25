using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ImageViewer.Runtime;

namespace ImageViewer.Services
{
    // 尝试直读资源管理器「搜索结果」窗口的图片列表（best-effort）：
    // 通过 Shell.Application 枚举已打开的 Explorer 窗口，若其位置是搜索虚拟文件夹（search-ms:），取其条目路径。
    // 系统/版本差异下可能失效，失败时静默回退（调用方转目录扫描）；本文件可整体移除而不影响其它功能。
    public static class SearchResultsProvider
    {
        private const string LoggerName = "ImageViewer";
        // 枚举 Explorer 窗口是同步 COM，搜索文件夹可能很大；超过预算即回退目录扫描，避免长时间卡住 UI。
        private static readonly TimeSpan TimeBudget = TimeSpan.FromMilliseconds(300);

        public static bool TryGetSearchResultImages(string openedPath, out IReadOnlyList<string> images)
        {
            images = null;
            if (String.IsNullOrWhiteSpace(openedPath)) return false;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic windows = shell.Windows();
                var count = (int)windows.Count;

                for (var i = 0; i < count; i++)
                {
                    if (stopwatch.Elapsed > TimeBudget)
                    {
                        Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "读取资源管理器搜索结果超时，回退目录扫描", null);
                        return false;
                    }

                    dynamic window = null;
                    try { window = windows.Item(i); }
                    catch { continue; }
                    if (window == null) continue;

                    string location = null;
                    try { location = (string)window.LocationURL; }
                    catch { continue; }
                    if (String.IsNullOrWhiteSpace(location)) continue;
                    // 搜索结果窗口的虚拟位置形如 search-ms:...
                    if (!location.StartsWith("search-ms:", StringComparison.OrdinalIgnoreCase)) continue;

                    List<string> list = EnumerateFolder(shell, location, stopwatch);
                    if (list == null) return false;
                    if (list.Count <= 1) continue;
                    if (!list.Any(path => String.Equals(path, openedPath, StringComparison.OrdinalIgnoreCase))) continue;

                    images = list;
                    Diagnostics.Sink.Log(LogSeverity.Info, LoggerName, "已从资源管理器搜索结果读取 " + list.Count + " 张图片", null);
                    return true;
                }
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "读取资源管理器搜索结果失败，回退目录扫描", error);
            }

            return false;
        }

        // 超预算返回 null，调用方据此回退目录扫描。
        private static List<string> EnumerateFolder(dynamic shell, string location, Stopwatch stopwatch)
        {
            var result = new List<string>();
            dynamic folder = null;
            try { folder = shell.NameSpace(location); }
            catch { return result; }
            if (folder == null) return result;

            dynamic items = folder.Items();
            var count = (int)items.Count;
            for (var i = 0; i < count; i++)
            {
                if (stopwatch.Elapsed > TimeBudget)
                {
                    Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "读取资源管理器搜索结果超时，回退目录扫描", null);
                    return null;
                }

                string path = null;
                try { path = (string)items.Item(i).Path; }
                catch { continue; }
                if (!String.IsNullOrWhiteSpace(path) && ImageFileWhitelist.IsWhitelisted(path)) result.Add(path);
            }
            return result;
        }
    }
}
