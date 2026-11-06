using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;
using KayoneZSoundpad.Services;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace KayoneZSoundpad.Audio;

/// <summary>Yeniden adlandırma sonucu.</summary>
public sealed record EndpointRenameResult(bool Success, bool AlreadyNamed, bool RestartRequired, string? Error);

/// <summary>
/// Windows ses uç noktalarının görünen adını değiştirir.
///
/// Yöntem: Windows'un kendi Ses ayarları ekranının kullandığı belgelenmemiş
/// <c>IPolicyConfig::SetPropertyValue</c> arayüzü (4 argümanlı "fxStore"
/// imzası — vtable'da <c>ResetDeviceFormat</c> dahil). Uç nokta özellik deposu
/// yönetici izni istediği için işlem, uygulamanın kendisini
/// <c>--rename-audio-endpoints</c> anahtarıyla yükseltilmiş olarak çalıştırmasıyla
/// yapılır.
/// </summary>
public static class AudioEndpointNamingService
{
    public const string RenameSwitch = "--rename-audio-endpoints";
    public const string OutName = "Voxora Out Audio";
    public const string InName = "Voxora In Audio";

    private const int EAccessDenied = unchecked((int)0x80070005);
    private static readonly Guid FriendlyNameFormatId = new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private const uint FriendlyNamePid = 14;
    private const ushort VtLpwstr = 31;

    public static string LogPath => Path.Combine(Path.GetTempPath(), "voxora-rename.log");

    // ------------------------------------------------------------------ akış

    /// <summary>
    /// Önce normal kullanıcı olarak dener (bazı sistemlerde izin verilir), olmazsa
    /// onay alıp yükseltilmiş yardımcıyı çalıştırır ve sonucu gerçek adlarla doğrular.
    /// </summary>
    public static async Task<EndpointRenameResult> RenameVoxoraEndpointsAsync(Window? owner)
    {
        var devices = FindCableDevices();
        if (devices is null)
            return new EndpointRenameResult(false, false, false, LocalizationService.T("DeviceName_NotFound"));

        var (cableIn, cableOut) = devices.Value;
        if (NamesApplied())
            return new EndpointRenameResult(true, true, false, null);

        // 1) Doğrudan (yükseltmesiz) deneme — bazı makinelerde policy izni vardır.
        TryRenameBoth(cableOut.Id, cableIn.Id, out _, out var directHr);
        if (directHr == 0 && await WaitForNamesAsync(1500))
            return new EndpointRenameResult(true, false, false, null);

        // 2) Kullanıcı onayı + UAC.
        var answer = MessageBox.Show(
            LocalizationService.T("DeviceName_Confirm"),
            LocalizationService.T("DeviceName_Title"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information,
            MessageBoxResult.OK);
        if (answer != MessageBoxResult.OK)
            return new EndpointRenameResult(false, false, false, null);

        int exitCode;
        try
        {
            exitCode = await RunElevatedHelperAsync();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new EndpointRenameResult(false, false, false, LocalizationService.T("DeviceName_UacDenied"));
        }
        catch (Exception ex)
        {
            return new EndpointRenameResult(false, false, false,
                LocalizationService.T("DeviceName_Failed", ex.Message));
        }

        // 3) Adlar servis tarafından hemen yayınlanmayabilir; doğrula.
        if (await WaitForNamesAsync(2500))
            return new EndpointRenameResult(true, false, false, null);

        if (exitCode == 2)
            return new EndpointRenameResult(false, false, false, LocalizationService.T("DeviceName_NotFound"));

        if (exitCode == 3)
            return new EndpointRenameResult(false, false, false, LocalizationService.T("DeviceName_AccessDenied"));

        return new EndpointRenameResult(false, false, false,
            LocalizationService.T("DeviceName_Failed", LastLogHint() ?? $"exit={exitCode}"));
    }

    private static (DeviceEntry In, DeviceEntry Out)? FindCableDevices()
    {
        var devices = new DeviceService();
        try
        {
            var cableIn = devices.FindCableRender();
            var cableOut = devices.FindCableCapture();
            if (cableIn is null || cableOut is null) return null;
            return (cableIn, cableOut);
        }
        finally
        {
            devices.Dispose();
        }
    }

    // ------------------------------------------------------------------ yardımcı

    /// <summary>Yükseltilmiş süreçte çalışır. 0 = değişti, 2 = kablo yok, 3 = izin yok, 4 = başka hata.</summary>
    public static int RenameElevated()
    {
        Log($"=== elevated start admin={IsAdministrator()} ===");
        try
        {
            var found = FindCableDevices();
            if (found is null)
            {
                Log("cable endpoints not found");
                return 2;
            }

            var (cableIn, cableOut) = found.Value;

            // 1) Policy config ile (Ses ayarlarıyla aynı yol).
            TryRenameBoth(cableOut.Id, cableIn.Id, out var hrOut, out var hrIn);

            if (hrOut == 0 && hrIn == 0 && WaitForNames(2000))
            {
                Log("policy rename verified");
                return 0;
            }

            // 2) Kayıt defteri + ses servisi (policy reddederse).
            Log("policy failed, trying registry fallback");
            var regOut = TryWriteRegistry(cableOut.Id, OutName);
            var regIn = TryWriteRegistry(cableIn.Id, InName);
            Log($"registry out={regOut} in={regIn}");

            if (regOut && regIn)
            {
                RestartAudioServices();
                if (WaitForNames(6000))
                {
                    Log("registry rename verified after service restart");
                    return 0;
                }
            }

            if (NamesApplied())
            {
                Log("names applied (late)");
                return 0;
            }

            var denied = hrOut == EAccessDenied || hrIn == EAccessDenied;
            Log($"rename failed hrOut=0x{hrOut:X8} hrIn=0x{hrIn:X8} denied={denied}");
            return denied ? 3 : 4;
        }
        catch (Exception ex)
        {
            Log("exception: " + ex);
            return 4;
        }
    }

    public static bool NamesApplied()
    {
        var devices = new DeviceService();
        try
        {
            var cableIn = devices.FindCableRender();
            var cableOut = devices.FindCableCapture();
            return cableIn != null && cableOut != null
                && IsVoxoraName(cableIn.OriginalName) && IsVoxoraName(cableOut.OriginalName);
        }
        finally
        {
            devices.Dispose();
        }
    }

    private static async Task<bool> WaitForNamesAsync(int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        do
        {
            if (NamesApplied()) return true;
            await Task.Delay(250);
        } while (Environment.TickCount64 < deadline);
        return NamesApplied();
    }

    private static bool WaitForNames(int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        do
        {
            if (NamesApplied()) return true;
            Thread.Sleep(250);
        } while (Environment.TickCount64 < deadline);
        return NamesApplied();
    }

    internal static bool IsVoxoraName(string name)
        => name.Contains("voxora in audio", StringComparison.OrdinalIgnoreCase)
           || name.Contains("voxora out audio", StringComparison.OrdinalIgnoreCase);

    private static bool TryRenameBoth(string outId, string inId, out int outHr, out int inHr)
    {
        outHr = SetFriendlyName(outId, OutName);
        inHr = SetFriendlyName(inId, InName);
        return outHr == 0 && inHr == 0;
    }

    /// <summary>IPolicyConfig üzerinden PKEY_Device_FriendlyName yazar.</summary>
    private static int SetFriendlyName(string deviceId, string name)
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            var policy = (IPolicyConfig)new PolicyConfigClient();
            var key = new PropertyKey { FormatId = FriendlyNameFormatId, PropertyId = FriendlyNamePid };
            ptr = Marshal.StringToCoTaskMemUni(name);
            var value = new PropVariant { VariantType = VtLpwstr, Pointer = ptr };

            int hr = policy.SetPropertyValue(deviceId, false, ref key, ref value);
            Log($"SetPropertyValue({Short(deviceId)}, '{name}') hr=0x{hr:X8}");
            return hr;
        }
        catch (Exception ex)
        {
            Log($"SetPropertyValue({Short(deviceId)}) exception: {ex.Message}");
            return unchecked((int)0x80004005);
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(ptr);
        }
    }

    // ------------------------------------------------------------------ kayıt defteri yedeği

    private static bool TryWriteRegistry(string deviceId, string name)
    {
        try
        {
            int start = deviceId.LastIndexOf('{');
            int end = deviceId.LastIndexOf('}');
            if (start < 0 || end <= start) return false;
            var endpointGuid = deviceId[(start + 1)..end];
            var flow = deviceId.Contains("{0.0.0.", StringComparison.Ordinal) ? "Render" : "Capture";
            var path = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\{flow}\{{{endpointGuid}}}\Properties";

            using var key = Registry.LocalMachine.OpenSubKey(path,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.SetValue | RegistryRights.ReadPermissions | RegistryRights.ChangePermissions);
            if (key == null)
            {
                Log($"registry key not found: {path}");
                return false;
            }

            key.SetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},14", name, RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            Log("registry write failed: " + ex.Message);
            return false;
        }
    }

    private static void RestartAudioServices()
    {
        try
        {
            Log("restarting audio services");
            Sc("stop Audiosrv");
            Sc("stop AudioEndpointBuilder");
            Thread.Sleep(600);
            Sc("start AudioEndpointBuilder");
            Thread.Sleep(800);
            Sc("start Audiosrv");
            Thread.Sleep(800);
            Log("audio services restarted");
        }
        catch (Exception ex)
        {
            Log("service restart failed: " + ex.Message);
        }
    }

    private static void Sc(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("sc.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(25000);
        }
        catch (Exception ex)
        {
            Log($"sc {arguments} failed: {ex.Message}");
        }
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    // ------------------------------------------------------------------ süreç

    private static async Task<int> RunElevatedHelperAsync()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            throw new InvalidOperationException("Executable path is unavailable.");

        var info = new ProcessStartInfo(exe, RenameSwitch)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Elevated process could not be started.");

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    // ------------------------------------------------------------------ log

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath,
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    private static string? LastLogHint()
    {
        try
        {
            if (!File.Exists(LogPath)) return null;
            var lines = File.ReadAllLines(LogPath);
            return lines.Length == 0 ? null : lines[^1];
        }
        catch { return null; }
    }

    private static string Short(string deviceId)
        => deviceId.Length <= 12 ? deviceId : "…" + deviceId[^10..];

    // ------------------------------------------------------------------ COM

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort VariantType;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Pointer;
        public IntPtr Pointer2;
        public IntPtr Pointer3;
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient { }

    /// <summary>
    /// Windows 7+ vtable: ResetDeviceFormat 3. slot; Get/SetPropertyValue ek
    /// <c>bool fxStore</c> parametresi alır. Bu imza olmadan çağrılar S_OK dönüp
    /// hiçbir şey yazmıyordu (yaşanmış hata).
    /// </summary>
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, out IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, out long period, out long minPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, ref long period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id,
            [MarshalAs(UnmanagedType.Bool)] bool fxStore, ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id,
            [MarshalAs(UnmanagedType.Bool)] bool fxStore, ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }
}
