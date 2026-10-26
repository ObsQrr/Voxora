using Microsoft.Win32;

namespace KayoneZSoundpad.Services;

/// <summary>Windows açılışında başlatma (HKCU Run anahtarı).</summary>
public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Voxora";

    // Eski ürün adıyla yazılmış kayıt (varsa temizlenir — geriye dönük uyumluluk)
    private const string LegacyValueName = "KayoneZSoundpad";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string || key?.GetValue(LegacyValueName) is string;
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return;
                key.SetValue(ValueName, $"\"{exe}\"");
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            }
        }
        catch { /* kayıt defteri erişilemiyorsa sessizce geç */ }
    }
}
