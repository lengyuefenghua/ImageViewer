using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using ImageViewer.Runtime;

namespace ImageViewer.Services
{
    // Windows 用 UserChoice 里的 Hash 判断「默认程序是用户本人选的」；算法未公开，这里移植自开源项目 PS-SFTA（MIT）。
    // 不变量：Hash 输入 = 扩展名 + 用户 SID + ProgId + 本地时间（截到分钟）的 FILETIME 十六进制 + shell32 里的体验字符串，
    // 全部小写；其中体验字符串随 Windows 版本变化，读取失败时回退到已知常量。
    internal static class UserChoiceHash
    {
        private const string UserExperienceSearch = "User Choice set via Windows User Experience";
        private const string FallbackUserExperience = "User Choice set via Windows User Experience {D18B6DD5-6124-4341-9318-804003BAFA0B}";
        private static readonly Lazy<string> userExperience = new Lazy<string>(DetectUserExperience);

        internal static string UserExperience { get { return userExperience.Value; } }

        internal static string GetUserSid()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return identity.User == null ? null : identity.User.Value.ToLowerInvariant();
            }
        }

        // 时间截到分钟：Windows 写入 UserChoice 时用的就是这个粒度，同一分钟内重复设置得到相同哈希。
        internal static string BuildBaseInfo(string extension, string userSid, string progId, DateTime localTime, string experience)
        {
            var minute = new DateTime(localTime.Year, localTime.Month, localTime.Day, localTime.Hour, localTime.Minute, 0, localTime.Kind);
            var fileTime = minute.ToFileTime();
            var timestamp = (fileTime >> 32).ToString("x8") + (fileTime & 0xFFFFFFFFL).ToString("x8");
            return (extension + userSid + progId + timestamp + experience).ToLowerInvariant();
        }

        // 自定义混合算法：MD5 作为种子，两轮 32 位整数混合后取 8 字节 Base64。
        internal static string Compute(string baseInfo)
        {
            if (String.IsNullOrEmpty(baseInfo)) return String.Empty;

            var bytes = Encoding.Unicode.GetBytes(baseInfo);
            var input = new byte[bytes.Length + 2];
            Buffer.BlockCopy(bytes, 0, input, 0, bytes.Length);

            byte[] md5;
            using (var hasher = MD5.Create())
            {
                md5 = hasher.ComputeHash(input);
            }

            var lengthBase = baseInfo.Length * 2 + 2;
            var length = ((lengthBase & 4) == 0 ? 1 : 0) + (lengthBase >> 2) - 1;
            if (length <= 1) return String.Empty;

            var counter = LogicalShiftRight(length - 2, 1) + 1;
            var pdata = 0;

            var md51 = unchecked((ToInt32(md5, 0) | 1) + 0x69FB0000L);
            var md52 = unchecked((ToInt32(md5, 4) | 1) + 0x13DB0000L);

            var cache = 0L;
            var outHash1 = 0;
            var outHash2 = 0;
            for (var i = 0; i < counter; i++)
            {
                var r0 = unchecked(ToInt32(input, pdata) + outHash1);
                var r1 = ToInt32(input, pdata + 4);
                pdata += 8;
                var r2 = unchecked((int)(r0 * md51 - 0x10FA9605L * ShiftRight16(r0)));
                var r2b = unchecked((int)(0x79F8A395L * r2 + 0x689B6B9FL * ShiftRight16(r2)));
                var r3 = unchecked((int)(0xEA970001L * r2b - 0x3C101569L * ShiftRight16(r2b)));
                var r4 = unchecked(r3 + r1);
                var r5 = unchecked((int)cache + r3);
                var r6 = unchecked((int)((long)r4 * md52 - 0x3CE8EC25L * ShiftRight16(r4)));
                var r6b = unchecked((int)(0x59C3AF2DL * r6 - 0x2232E0F1L * ShiftRight16(r6)));
                outHash1 = unchecked((int)(0x1EC90001L * r6b + 0x35BD1EC9L * ShiftRight16(r6b)));
                outHash2 = unchecked(r5 + outHash1);
                cache = outHash2;
            }

            var outHash = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(outHash1), 0, outHash, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(outHash2), 0, outHash, 4, 4);

            var md51Low = unchecked(ToInt32(md5, 0) | 1);
            var md52Low = unchecked(ToInt32(md5, 4) | 1);

            cache = 0L;
            outHash1 = 0;
            outHash2 = 0;
            pdata = 0;
            counter = LogicalShiftRight(length - 2, 1) + 1;
            for (var i = 0; i < counter; i++)
            {
                var r0 = unchecked(ToInt32(input, pdata) + outHash1);
                pdata += 8;
                var r1 = unchecked((int)((long)r0 * md51Low));
                var r1b = unchecked((int)(0xB1110000L * r1 - 0x30674EEFL * ShiftRight16(r1)));
                var r2 = unchecked((int)(0x5B9F0000L * r1b - 0x78F7A461L * ShiftRight16(r1b)));
                var r2b = unchecked((int)(0x12CEB96DL * ShiftRight16(r2) - 0x46930000L * r2));
                var r3 = unchecked((int)(0x1D830000L * r2b + 0x257E1D83L * ShiftRight16(r2b)));
                var r4 = unchecked((int)((long)md52Low * ((long)r3 + ToInt32(input, pdata - 4))));
                var r4b = unchecked((int)(0x16F50000L * r4 - 0x5D8BE90BL * ShiftRight16(r4)));
                var r5 = unchecked((int)(0x96FF0000L * r4b - 0x2C7C6901L * ShiftRight16(r4b)));
                var r5b = unchecked((int)(0x2B890000L * r5 + 0x7C932B89L * ShiftRight16(r5)));
                outHash1 = unchecked((int)(0x9F690000L * r5b - 0x405B6097L * ShiftRight16(r5b)));
                outHash2 = unchecked((int)((long)outHash1 + cache + r3));
                cache = outHash2;
            }

            Buffer.BlockCopy(BitConverter.GetBytes(outHash1), 0, outHash, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(outHash2), 0, outHash, 12, 4);

            var result = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(ToInt32(outHash, 8) ^ ToInt32(outHash, 0)), 0, result, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ToInt32(outHash, 12) ^ ToInt32(outHash, 4)), 0, result, 4, 4);
            return Convert.ToBase64String(result);
        }

        private static int ToInt32(byte[] bytes, int index)
        {
            return BitConverter.ToInt32(bytes, index);
        }

        // 对应 PS-SFTA 的 Get-ShiftRight：按 32 位无符号右移处理负数。
        private static int ShiftRight16(int value)
        {
            return (int)((uint)value >> 16);
        }

        private static int LogicalShiftRight(int value, int count)
        {
            return (int)((uint)value >> count);
        }

        private static string DetectUserExperience()
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var buffer = new byte[5 * 1024 * 1024];
                    var read = stream.Read(buffer, 0, buffer.Length);
                    var text = Encoding.Unicode.GetString(buffer, 0, read);
                    var start = text.IndexOf(UserExperienceSearch, StringComparison.Ordinal);
                    var end = start < 0 ? -1 : text.IndexOf('}', start);
                    if (start >= 0 && end > start)
                    {
                        return text.Substring(start, end - start + 1);
                    }
                }
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "读取 UserChoice 体验字符串失败，使用内置常量", error);
            }

            return FallbackUserExperience;
        }
    }
}
