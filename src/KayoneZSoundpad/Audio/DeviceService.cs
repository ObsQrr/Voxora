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
    private readonly object _notifyGate = new();
    private System.Threading.Timer? _notifyTimer;
    private string? _lastSignature;
    private int _disposed;
    private readonly SynchronizationContext? _sync;

    /// <summary>
    /// Görünür aygıt kümesi gerçekten değiştiğinde tetiklenir. Windows bazı
    /// sürücülerde aynı olayı saniyede birkaç kez yayınlıyor; bu yüzden olaylar
    /// 450 ms'de birleştirilir ve imza değişmediyse hiç yayınlanmaz.
    /// </summary>
    public event Action? DevicesChanged;

    public DeviceService()
    {
        // Core Audio nesneleri oluşturuldukları bağlamda kullanılmalı; imza
        // kontrolünü UI bağlamına geri gönder (yoksa satır içi çalışır).
        _sync = SynchronizationContext.Current;
        _notificationClient = new DeviceNotificationClient(ScheduleNotify);
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
        _lastSignature = BuildSignature();
    }

    /// <summary>Olay fırtınasını tek bir gecikmeli kontrole indirger.</summary>
    private void ScheduleNotify()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        lock (_notifyGate)
        {
            _notifyTimer ??= new System.Threading.Timer(_ => PublishIfChanged(), null,
                Timeout.Infinite, Timeout.Infinite);
            _notifyTimer.Change(450, Timeout.Infinite);
        }
    }

    private void PublishIfChanged()
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        if (_sync != null)
        {
            try
            {
                _sync.Post(_ => PublishIfChangedCore(), null);
                return;
            }
            catch { /* bağlam kapanmışsa satır içi dene */ }
        }

        PublishIfChangedCore();
    }

    private void PublishIfChangedCore()
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        string signature;
        try { signature = BuildSignature(); }
        catch { return; }

        lock (_notifyGate)
        {
            if (string.Equals(signature, _lastSignature, StringComparison.Ordinal)) return;
            _lastSignature = signature;
        }

        try { DevicesChanged?.Invoke(); } catch { }
    }

    /// <summary>Aktif uç noktaların kimlik + görünen ad + varsayılan özeti.</summary>
    private string BuildSignature()
    {
        var sb = new System.Text.StringBuilder(256);
        foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
        {
            sb.Append(flow).Append(':');

            string? defaultId = null;
            NAudio.CoreAudioApi.MMDevice? defaultDevice = null;
            try
            {
                defaultDevice = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                defaultId = defaultDevice?.ID;
            }
            catch { }
            finally { try { defaultDevice?.Dispose(); } catch { } }

            try
            {
                foreach (var device in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                {
                    try
                    {
                        sb.Append(device.ID).Append('|');
                        try { sb.Append(device.FriendlyName); } catch { }
                        sb.Append(';');
                    }
                    catch { }
                    finally { try { device.Dispose(); } catch { } }
                }
            }
            catch { }

            sb.Append("#default=").Append(defaultId).Append('\n');
        }
        return sb.ToString();
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
            NAudio.CoreAudioApi.MMDevice? defaultDevice = null;
            try
            {
                defaultDevice = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                defaultId = defaultDevice?.ID;
            }
            catch { }
            finally { try { defaultDevice?.Dispose(); } catch { } }

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
        if (flow == DataFlow.Render && (lower.Contains("cable input") || lower.Contains("voxora in audio")))
            return "Voxora In Audio";

        // Giriş aygıtları (Capture) - Sanal kablodan Discord'a/oyuna giden ses
        if (flow == DataFlow.Capture && (lower.Contains("cable output") || lower.Contains("voxora out audio")))
            return "Voxora Out Audio";

        return originalName;
    }

    public static bool IsCableName(string name)
    {
        name = name.ToLowerInvariant();
        return name.Contains("cable input") || name.Contains("cable output") ||
               name.Contains("cable-audio") || name.Contains("virtual cable") ||
               name.Contains("voxora in audio") || name.Contains("voxora out audio");
    }

    /// <summary>
    /// VB-CABLE kurulu mu? (CABLE Output yakalama aygıtı aranır)
    /// ÖNEMLİ: Gösterim adı Voxora markalı olduğu için eşleştirme daima OriginalName üzerinden yapılır.
    /// </summary>
    public DeviceEntry? FindCableCapture()
    {
        var captures = GetCaptureDevices();
        return captures.FirstOrDefault(d => d.IsCable && (
                   d.OriginalName.Contains("cable output", StringComparison.OrdinalIgnoreCase) ||
                   d.OriginalName.Contains("voxora out audio", StringComparison.OrdinalIgnoreCase)))
            ?? captures.FirstOrDefault(d => d.IsCable);
    }

    public DeviceEntry? FindCableRender()
    {
        var renders = GetRenderDevices();
        return renders.FirstOrDefault(d => d.IsCable && (
                   d.OriginalName.Contains("cable input", StringComparison.OrdinalIgnoreCase) ||
                   d.OriginalName.Contains("voxora in audio", StringComparison.OrdinalIgnoreCase)))
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        lock (_notifyGate)
        {
            _notifyTimer?.Dispose();
            _notifyTimer = null;
        }

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

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            // Yalnızca görünen ad değiştiğinde listeyi tazele (ses seviyesi vb. değil).
            if (key.formatId == FriendlyNameFormatId && key.propertyId == FriendlyNamePropertyId)
                Notify();
        }

        private static readonly Guid FriendlyNameFormatId = new("a45c254e-df1c-4efd-8020-67d146a850e0");
        private const int FriendlyNamePropertyId = 14;

        private void Notify() => _onChanged();
    }
}

