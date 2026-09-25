using System;
using ImageViewer.Services;

namespace ImageViewer.Services
{
    public enum ElevatedAction
    {
        None,
        Register,
        Unregister
    }

    // 提权启动参数识别：runas 子进程据此执行 HKLM 注册/取消，不再走正常启动流程。
    public static class ElevatedCommand
    {
        public static ElevatedAction Parse(string[] args)
        {
            if (args == null) return ElevatedAction.None;

            foreach (var argument in args)
            {
                if (String.Equals(argument, FileAssociationRegistrar.RegisterArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return ElevatedAction.Register;
                }
                if (String.Equals(argument, FileAssociationRegistrar.UnregisterArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return ElevatedAction.Unregister;
                }
            }

            return ElevatedAction.None;
        }
    }
}
