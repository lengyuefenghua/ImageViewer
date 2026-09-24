using System;
using System.IO;
using ImageViewer.Core.Diagnostics;

namespace ImageViewer.App.Configuration
{
    public static class AtomicJsonFile
    {
        private const string LoggerName = "ImageViewer";

        public static void Save(string path, string content, Action<string> validate = null)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("配置文件路径不能为空。", "path");
            if (content == null) throw new ArgumentNullException("content");

            var directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (validate != null) validate(File.ReadAllText(temporaryPath));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            catch (Exception error)
            {
                // 配置写入失败必须留痕；保持原有异常语义，由调用方决定处理。
                Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "配置文件写入失败：" + path, error);
                throw;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
