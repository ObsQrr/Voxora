using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Audio;

/// <summary>UI'da gösterilecek ses aygıtı bilgisi.</summary>
public class DeviceEntry
{
    public string Id { get; init; } = "";
    /// <summary>Windows'un gerçek (yerelleştirilmiş) cihaz adı — eşleştirme için kullanılır.</summary>
    public string OriginalName { get; init; } = "";
    /// <summary>Kullanıcıya gösterilen ad (VB-CABLE için Voxora markalı).</summary>
    public string Name { get; init; } = "";
    public bool IsDefault { get; init; }
    public bool IsCable { get; init; }

    /// <summary>ComboBox'larda gösterilen metin: ad + varsayılan işareti (dile duyarlı).</summary>
    public string DisplayName => Name + (IsDefault ? LocalizationService.T("Device_DefaultSuffix") : "");

    public override string ToString() => DisplayName;
}

/// <summary>Windows ses aygıtı listesi, VB-CABLE tespiti ve aygıt değişikliği bildirimleri.</summary>
public class DeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private DeviceNotificationClient? _notificationClient;

    /// <summary>Aygıt eklendi/kaldırıldı/devre dışı kaldı.</summary>
    public event Action? DevicesChanged;

    public DeviceService()
    {
        _notificationClient = new DeviceNotificationClient(() => DevicesChanged?.Invoke());
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
    }

    public List<DeviceEntry> GetRenderDevices()
        => GetDevices(DataFlow.Render);

    public List<DeviceEntry> GetCaptureDevices()
        => GetDevices(DataFlow.Capture);

    private List<DeviceEntry> GetDevices(DataFlow flow)
    {
        var list = new List<DeviceEntry>();
        try
        {
            string? defaultId = null;
            try { defaultId = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia)?.ID; } catch { }

            foreach (var d in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                string originalName;
                try { originalName = d.FriendlyName; } catch { originalName = LocalizationService.T("Device_UnknownName"); }

                // Kullanıcıya gösterilecek ad: VB-CABLE aygıtları için Voxora markalı isimler
                string displayName = GetDisplayName(flow, originalName);

                list.Add(new DeviceEntry
                {
                    Id = d.ID,
                    OriginalName = originalName,
                    Name = displayName,
                    IsDefault = d.ID == defaultId,
                    IsCable = IsCableName(originalName)
                });
                d.Dispose();
            }
        }
        catch { /* aygıt listesi alınamadıysa boş dön */ }
        return list.OrderBy(x => x.IsCable ? 0 : 1).ThenBy(x => x.Name).ToList();
    }

    /// <summary>
    /// Dahili cihaz adını kullanıcı dostu gösterim adına çevirir.
    /// Orijinal isim teknik eşleştirme için korunur (IsCableName orijinal ismi kullanır).
    /// </summary>
    internal static string GetDisplayName(DataFlow flow, string originalName)
    {
        var lower = originalName.ToLowerInvariant();

        // Çıkış aygıtları (Render) - Voxora'dan sanal kabloya giden ses
        if (flow == DataFlow.Render && lower.Contains("cable input"))
            return "Voxora In Audio";

        // Giriş aygıtları (Capture) - Sanal kablodan Discord'a/oyuna giden ses
        if (flow == DataFlow.Capture && lower.Contains("cable output"))
            return "Voxora Out Audio";

        return originalName;
    }

    public static bool IsCableName(string name)
    {
        name = name.ToLowerInvariant();
        return name.Contains("cable input") || name.Contains("cable output") ||
               name.Contains("cable-audio") || name.Contains("virtual cable");
    }

    /// <summary>
    /// VB-CABLE kurulu mu? (CABLE Output yakalama aygıtı aranır)
    /// ÖNEMLİ: Gösterim adı Voxora markalı olduğu için eşleştirme daima OriginalName üzerinden yapılır.
    /// </summary>
    public DeviceEntry? FindCableCapture()
    {
        var captures = GetCaptureDevices();
        return captures.FirstOrDefault(d => d.IsCable && d.OriginalName.ToLowerInvariant().Contains("cable output"))
            ?? captures.FirstOrDefault(d => d.IsCable);
    }

    public DeviceEntry? FindCableRender()
    {
        var renders = GetRenderDevices();
        return renders.FirstOrDefault(d => d.IsCable && d.OriginalName.ToLowerInvariant().Contains("cable input"))
            ?? renders.FirstOrDefault(d => d.IsCable);
    }

    public DeviceEntry? FindById(DataFlow flow, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return flow == DataFlow.Render ? GetRenderDevices().FirstOrDefault(d => d.Id == id)
                                       : GetCaptureDevices().FirstOrDefault(d => d.Id == id);
    }

    public string? GetDefaultRenderId()
    {
        try { return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)?.ID; }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_notificationClient != null)
        {
            try { _enumerator.UnregisterEndpointNotificationCallback(_notificationClient); } catch { }
            _notificationClient = null;
        }
        try { _enumerator.Dispose(); } catch { }
    }

    /// <summary>MMDeviceEnumerator'ın aygıt değişikliği bildirimini Action'a çevirir.</summary>
    private class DeviceNotificationClient : IMMNotificationClient
    {
        private readonly Action _onChanged;

        public DeviceNotificationClient(Action onChanged) => _onChanged = onChanged;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => Notify();
        public void OnDeviceAdded(string pwstrDeviceName) => Notify();
        public void OnDeviceRemoved(string deviceId) => Notify();
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) => Notify();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { /* yok say */ }

        private void Notify() => _onChanged();
    }
}

