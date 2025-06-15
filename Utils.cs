// Utils.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.Win32;

namespace tempCPU
{
    public static class Utils
    {
        public static bool IsRunAsAdmin()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        public static void RelaunchAsAdmin()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            Process.Start(new ProcessStartInfo(exePath) { Verb = "runas" });
        }

        public static void AddToStartup()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            using var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            rk?.SetValue("CpuTempTrayWpf", exePath);
        }

        public static void SaveHotKeyToRegistry(uint vk)
        {
            using var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CpuTempTrayWpf");
            rk?.SetValue("HotKey", vk, RegistryValueKind.DWord);
        }

        public static uint LoadHotKeyFromRegistry()
        {
            using var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CpuTempTrayWpf");
            var value = rk?.GetValue("HotKey");
            if (value != null && uint.TryParse(value.ToString(), out uint vk))
                return vk;
            return 0x6B; // VK_ADD
        }

        public static Icon LoadAppIconSafe()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Icon.ico");
                if (File.Exists(iconPath))
                    return new Icon(iconPath);
            }
            catch { }
            return SystemIcons.Information;
        }
    }
}
