using System;
using System.IO;
using System.Threading.Tasks;
using ImageViewer.Runtime;

namespace ImageViewer.Services
{
    public static class ImageCopyService
    {
        private const string LoggerName = "ImageViewer";

        // 异步复制：冲突判定仍在调用线程（UI）同步询问，实际文件拷贝放到线程池，避免复制大图时冻结界面。
        public static async Task<CopyResult> CopyAsync(
            string sourcePath,
            string targetDirectory,
            string fileName,
            CopyConflict conflict,
            Func<string, CopyDecision> resolveConflict)
        {
            if (String.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("源文件不能为空。", "sourcePath");
            if (String.IsNullOrWhiteSpace(targetDirectory)) throw new ArgumentException("目标目录不能为空。", "targetDirectory");
            if (String.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("文件名不能为空。", "fileName");
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("源图片不存在：" + sourcePath, sourcePath);

            Directory.CreateDirectory(targetDirectory);
            var destination = Path.Combine(targetDirectory, fileName);

            if (File.Exists(destination))
            {
                var decision = conflict == CopyConflict.Ask
                    ? (resolveConflict != null ? resolveConflict(destination) : CopyDecision.Skip)
                    : (conflict == CopyConflict.Overwrite ? CopyDecision.Overwrite : CopyDecision.Skip);

                if (decision == CopyDecision.Cancel)
                {
                    Diagnostics.Sink.Log(LogSeverity.Info, LoggerName, "复制已取消：" + destination, null);
                    return CopyResult.Cancelled;
                }
                if (decision == CopyDecision.Skip)
                {
                    Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "复制跳过（目标已存在）：" + destination, null);
                    return CopyResult.Skipped;
                }
            }

            await Task.Run(() => File.Copy(sourcePath, destination, true)).ConfigureAwait(true);
            Diagnostics.Sink.Log(LogSeverity.Info, LoggerName, "已复制图片：" + sourcePath + " -> " + destination, null);
            return CopyResult.Copied;
        }
    }
}
