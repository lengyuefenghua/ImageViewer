using System.Collections.Generic;

namespace ImageViewer.Viewer
{
    // 「复制到」目标：每行一个目录，附带原样拼在扩展名前的后缀。
    public sealed class CopyTarget
    {
        public string Path { get; set; }
        public string Suffix { get; set; }
    }

    // 同名冲突策略（全局配置）。
    public enum CopyConflict
    {
        Ask,
        Overwrite,
        Skip
    }

    // 冲突询问时用户的即时选择（非配置）。
    public enum CopyDecision
    {
        Overwrite,
        Skip,
        Cancel
    }

    public enum CopyResult
    {
        Copied,
        Skipped,
        Cancelled
    }

    public sealed class CopySettings
    {
        public const string DefaultShortcut = "Space";

        public CopySettings()
        {
            Targets = new List<CopyTarget>();
            TimestampFormat = "";
            Conflict = CopyConflict.Ask;
            Shortcut = DefaultShortcut;
        }

        public IList<CopyTarget> Targets { get; private set; }

        // 空字符串 = 不追加时间戳。
        public string TimestampFormat { get; set; }

        public CopyConflict Conflict { get; set; }

        public string Shortcut { get; set; }
    }
}
