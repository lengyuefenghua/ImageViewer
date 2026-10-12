using System.Collections;
using System.Resources;
using Mono.Cecil;

// 构建期工具：从 Wpf.Ui.dll 的内嵌资源里剔除未被使用的 Fluent 图标字体，缩小发布体积。
// 只删除资源条目、不改任何托管代码，程序集身份（PublicKeyToken）保持不变。
// 注意：Wpf.Ui.dll 带强名称签名，改写后签名失效，依赖 .NET Framework 默认的 strong-name bypass
// 才能加载（默认开启；极少数显式禁用该策略的机器上会失败，回退方案见 docs/changes）。

const string ResourceName = "Wpf.Ui.g.resources";
string[] fontKeys =
{
    "resources/fonts/fluentsystemicons-regular.ttf",
    "resources/fonts/fluentsystemicons-filled.ttf",
};

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: WpfUiFontStripper <path-to-Wpf.Ui.dll>");
    return 2;
}

string dllPath = Path.GetFullPath(args[0]);
if (!File.Exists(dllPath))
{
    Console.Error.WriteLine($"[strip] ERROR: file not found: {dllPath}");
    return 1;
}

long beforeSize = new FileInfo(dllPath).Length;

// 先整体读入内存再处理，避免 Cecil 持有文件句柄导致写回时被锁。
using var input = new MemoryStream(File.ReadAllBytes(dllPath));
using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(input);
ModuleDefinition module = assembly.MainModule;

EmbeddedResource? resource = module.Resources
    .OfType<EmbeddedResource>()
    .FirstOrDefault(r => r.Name == ResourceName);
if (resource is null)
{
    Console.Error.WriteLine($"[strip] ERROR: embedded resource '{ResourceName}' not found in {dllPath}");
    return 1;
}

byte[] original;
using (Stream stream = resource.GetResourceStream())
using (var buffer = new MemoryStream())
{
    stream.CopyTo(buffer);
    original = buffer.ToArray();
}

// 逐条读取，剔除字体条目后原样保留其余资源（BAML 样式/主题）再写回。
// 关键：WPF 通过 ResourceManager.GetStream 读取 BAML，条目必须保持原本的
// ResourceTypeCode.Stream（写成 ByteArray 会在加载主题字典时抛 InvalidOperationException），
// 因此这里连同“是否 Stream”一起保留。
var kept = new List<(string Key, byte[] Data, bool IsStream)>();
var removed = new List<string>();
using (var reader = new ResourceReader(new MemoryStream(original)))
{
    foreach (DictionaryEntry entry in reader)
    {
        if (entry.Key is not string key)
        {
            Console.Error.WriteLine("[strip] ERROR: non-string resource key encountered");
            return 1;
        }

        if (fontKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            removed.Add(key);
            continue;
        }

        byte[] data;
        bool isStream;
        switch (entry.Value)
        {
            case byte[] bytes:
                data = bytes;
                isStream = false;
                break;
            case Stream stream:
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    data = ms.ToArray();
                }

                isStream = true;
                break;
            default:
                Console.Error.WriteLine($"[strip] ERROR: unexpected non-binary resource '{key}'");
                return 1;
        }

        kept.Add((key, data, isStream));
    }
}

// 预期字体缺失说明 WPF-UI 资源布局变了，必须硬失败而不是静默产出没瘦身的包。
foreach (string expected in fontKeys)
{
    if (!removed.Contains(expected, StringComparer.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine(
            $"[strip] ERROR: expected font '{expected}' not found; WPF-UI resource layout changed, review this tool.");
        return 1;
    }
}

byte[] rewritten;
using (var buffer = new MemoryStream())
{
    using (var writer = new ResourceWriter(buffer))
    {
        foreach ((string key, byte[] data, bool isStream) in kept)
        {
            // 原样还原条目类型：Stream 写回 Stream，byte[] 写回 ByteArray。
            if (isStream)
            {
                writer.AddResource(key, new MemoryStream(data, writable: false));
            }
            else
            {
                writer.AddResource(key, data);
            }
        }
    }

    rewritten = buffer.ToArray();
}

module.Resources.Remove(resource);
module.Resources.Add(new EmbeddedResource(ResourceName, resource.Attributes, rewritten));

using (var output = new MemoryStream())
{
    assembly.Write(output);
    File.WriteAllBytes(dllPath, output.ToArray());
}

long afterSize = new FileInfo(dllPath).Length;
Console.WriteLine($"[strip] {Path.GetFileName(dllPath)}: {beforeSize} -> {afterSize} bytes (-{beforeSize - afterSize})");
Console.WriteLine($"[strip] removed {removed.Count}: {string.Join(", ", removed)}");
Console.WriteLine($"[strip] kept {kept.Count} resources");
return 0;
