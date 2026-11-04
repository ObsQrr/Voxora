using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using KayoneZSoundpad.Services;
using NAudio.CoreAudioApi;

namespace KayoneZSoundpad.Audio;

/// <summary>Yeniden adlandırma sonucu.</summary>
public sealed record EndpointRenameResult(bool Success, bool AlreadyNamed, bool RestartRequired, string? Error);

/// <summary>
/// Windows ses uç noktalarının görünen adını değiştirir. Uç nokta özellik
/// deposu yalnızca yönetici tarafından yazılabildiği için işlem, uygulamanın
/// kendisini <c>--rename-audio-endpoints</c> anahtarıyla yükseltilmiş olarak
/// çalıştırmasıyla yapılır. Ad değişikliği Discord dahil tüm uygulamalarda görünür.
/// </summary>
public static class AudioEndpointNamingService
{
    public const string RenameSwitch = "--rename-audio-endpoints";
    public const string OutName = "Voxora Out Audio";
    public const string InName = "Voxora In Audio";

    private static readonly Guid FriendlyNameFormatId = new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private const uint FriendlyNamePid = 14;
    private const int StgmReadWrite = 0x00000002;

    /// <summary>
    /// Kullanıcıya onay sorar, yükseltilmiş yardımcıyı çalıştırır ve sonucu
    /// gerçek cihaz adlarını yeniden okuyarak doğrular.
    /// </summary>
    public static async Task<EndpointRenameResult> RenameVoxoraEndpointsAsync(Window? owner)
    {
        var devices = new DeviceService();
        DeviceEntry? cableIn;
        DeviceEntry? cableOut;
        try
        {
            cableIn = devices.FindCableRender();
            cableOut = devices.FindCableCapture();
        }
        finally
        {
            devices.Dispose();
        }

        if (cableIn == null || cableOut == null)
            return new EndpointRenameResult(false, false, false, LocalizationService.T("DeviceName_NotFound"));

        if (IsVoxoraName(cableIn.OriginalName) && IsVoxoraName(cableOut.OriginalName))
            return new EndpointRenameResult(true, true, false, null);

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
            // Kullanıcı UAC penceresini kapattı.
            return new EndpointRenameResult(false, false, false, LocalizationService.T("DeviceName_UacDenied"));
        }
        catch (Exception ex)
        {
            return new EndpointRenameResult(false, false, false,
                LocalizationService.T("DeviceName_Failed", ex.Message));
        }

        if (exitCode == 2)
            return new EndpointRenameResult(false, false, false, LocalizationService.T("DeviceName_NotFound"));

        // Adlar servis tarafından hemen yayınlanmayabilir; kısa bir bekleme sonrası doğrula.
        await Task.Delay(350);
        if (NamesApplied())
            return new EndpointRenameResult(true, false, false, null);

        return exitCode == 0
            ? new EndpointRenameResult(true, false, true, null)
            : new EndpointRenameResult(false, false, false,
                LocalizationService.T("DeviceName_Failed", $"exit={exitCode}"));
    }

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

    /// <summary>Yükseltilmiş süreçte çalışır. 0 = değişti, 2 = kablo yok, 3 = izin yok.</summary>
    public static int RenameElevated()
    {
        try
        {
            var devices = new DeviceService();
            var cableIn = devices.FindCableRender();
            var cableOut = devices.FindCableCapture();
            devices.Dispose();

            if (cableIn == null || cableOut == null) return 2;

            var okOut = TrySetFriendlyName(cableOut.Id, OutName);
            var okIn = TrySetFriendlyName(cableIn.Id, InName);
            if (!okOut || !okIn) return 3;
            return 0;
        }
        catch
        {
            return 1;
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

    internal static bool IsVoxoraName(string name)
        => name.Contains("voxora in audio", StringComparison.OrdinalIgnoreCase)
           || name.Contains("voxora out audio", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Uç noktanın PKEY_Device_FriendlyName değerini yazar. Başarısız olursa
    /// kayıt defteri yolunu dener; her iki durumda da yalnızca ad değişir.
    /// </summary>
    internal static bool TrySetFriendlyName(string deviceId, string name)
    {
        if (TrySetThroughPropertyStore(deviceId, name)) return true;
        return TrySetThroughRegistry(deviceId, name);
    }

    private static bool TrySetThroughPropertyStore(string deviceId, string name)
    {
        IMMDevice? device = null;
        IPropertyStore? store = null;
        IntPtr valuePtr = IntPtr.Zero;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDevice(deviceId, out device) != 0 || device == null) return false;
            if (device.OpenPropertyStore(StgmReadWrite, out store) != 0 || store == null) return false;

            var key = new PropertyKey { FormatId = FriendlyNameFormatId, PropertyId = FriendlyNamePid };
            valuePtr = Marshal.StringToCoTaskMemUni(name);
            var value = new PropVariant { VariantType = 31, Pointer = valuePtr };

            if (store.SetValue(ref key, ref value) != 0) return false;
            return store.Commit() == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (valuePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(valuePtr);
            if (store != null) Marshal.ReleaseComObject(store);
            if (device != null) Marshal.ReleaseComObject(device);
        }
    }

    private static bool TrySetThroughRegistry(string deviceId, string name)
    {
        try
        {
            // "{0.0.1.00000000}.{guid}" → son süslü parantezli bölüm.
            int start = deviceId.LastIndexOf('{');
            int end = deviceId.LastIndexOf('}');
            if (start < 0 || end <= start) return false;
            var endpointGuid = deviceId[(start + 1)..end];

            var flow = deviceId.Contains("{0.0.0.", StringComparison.Ordinal) ? "Render" : "Capture";
            var path = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\{flow}\{{{endpointGuid}}}\Properties";

            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path, writable: true);
            if (key == null) return false;
            key.SetValue(
                "{a45c254e-df1c-4efd-8020-67d146a850e0},14",
                name,
                Microsoft.Win32.RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

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

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object? ppInterface);
        int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    private interface IPropertyStore
    {
        int GetCount(out int propertyCount);
        int GetAt(int property, out PropertyKey key);
        int GetValue(ref PropertyKey key, out PropVariant value);
        int SetValue(ref PropertyKey key, ref PropVariant value);
        int Commit();
    }
}
