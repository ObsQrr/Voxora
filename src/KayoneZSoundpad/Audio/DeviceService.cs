using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace KayoneZSoundpad.Audio;

/// <summary>UI'da gösterilecek ses aygıtı bilgisi.</summary>
public class DeviceEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsDefault { get; init; }
    public bool IsCable { get; init; }

    public override string ToString() => Name + (IsDefault ? "  (varsayılan)" : "");
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
                string name;
                try { name = d.FriendlyName; } catch { name = "(bilinmeyen aygıt)"; }
                list.Add(new DeviceEntry
                {
                    Id = d.ID,
                    Name = name,
                    IsDefault = d.ID == defaultId,
                    IsCable = IsCableName(name)
                });
                d.Dispose();
            }
        }
        catch { /* aygıt listesi alınamadıysa boş dön */ }
        return list.OrderBy(x => x.IsCable ? 0 : 1).ThenBy(x => x.Name).ToList();
    }

    public static bool IsCableName(string name)
    {
        name = name.ToLowerInvariant();
        return name.Contains("cable input") || name.Contains("cable output") ||
               name.Contains("cable-audio") || name.Contains("virtual cable");
    }

    /// <summary>VB-CABLE kurulu mu? (CABLE Output yakalama aygıtı aranır)</summary>
    public DeviceEntry? FindCableCapture()
        => GetCaptureDevices().FirstOrDefault(d => d.Name.ToLowerInvariant().Contains("cable output"));

    public DeviceEntry? FindCableRender()
        => GetRenderDevices().FirstOrDefault(d => d.Name.ToLowerInvariant().Contains("cable input"));

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

