using System;
using System.Web.Script.Serialization;

namespace ImageViewer.Services
{
    public static class JsonSerialization
    {
        // JavaScriptSerializer 非线程安全，用线程静态实例复用，避免每次调用都 new（构造有反射/元数据开销）。
        // 线程池线程数量有界，实例数随之有界；复用不改变序列化结果（逐字节与新建实例一致）。
        [ThreadStatic] private static JavaScriptSerializer serializer;

        public static T Deserialize<T>(string json) { return Instance.Deserialize<T>(json); }

        public static string Serialize<T>(T value) { return Instance.Serialize(value); }

        private static JavaScriptSerializer Instance
        {
            get { return serializer ?? (serializer = new JavaScriptSerializer()); }
        }
    }
}
