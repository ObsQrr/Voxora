using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KayoneZSoundpad.Audio;

/// <summary>Bir sesin motor içindeki aktif çalma kaydı.</summary>
public class PlaybackVoice
{
    public required Guid SoundId { get; init; }
    public required ISampleProvider Source { get; init; }
    public required VolumeSampleProvider Volume { get; init; }
    public bool IsPlaying { get; internal set; }
    public bool HoldRequested { get; set; } // basılı tutma modunda tuş hâlâ basılı mı
    public event Action<PlaybackVoice>? Stopped;

    internal void OnFinished()
    {
        IsPlaying = false;
        Stopped?.Invoke(this);
    }
}

/// <summary>
/// KayoneZ ses motoru.
///
/// Mimari:
///   [Efekt kaynakları] ──► fxMixer ──►(+FxFolume)─┬─► Çıkış aygıtı (örn. VB-CABLE "CABLE Input",
///   [Mikrofon capture] ──►(+MicVolume, duck)──────┘   Discord "CABLE Output"u mikrofon olarak kullanır)
///                          fxMixer ──►(+FxVolume)──► Monitör çıkışı (lokal hoparlörler)
///
/// Tek karıştırma iş parçacığı 10 ms'lik bloklarla çalışır; tüm akışlar 48 kHz/2ch float.
/// Çıkış tamponuna 100 ms önden besleme yapılır; taşmada eski veri atılır (DiscardOnBufferOverflow).
/// </summary>
public class AudioEngine : IDisposable
{
    public const int SampleRate = 48000;
    private const int Channels = 2;
    private const int MixIntervalMs = 10;
    private const int PrimeMs = 120;

    private static readonly WaveFormat Stereo48k = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    private readonly object _lock = new();

    // Aygıtlar / akışlar
    private MMDeviceEnumerator _enumerator = new();
    private WasapiOut? _outMain;          // sanal mikrofon girişi (örn. CABLE Input) veya seçilen çıkış
    private WasapiOut? _outMonitor;       // lokal monitör
    private WasapiCapture? _micCapture;
    private BufferedWaveProvider? _bufMain;
    private BufferedWaveProvider? _bufMonitor;
    private BufferedWaveProvider? _bufMic;
    private ISampleProvider? _micProvider;
    private MixingSampleProvider? _fxMixer;

    // Karıştırma iş parçacığı
    private Thread? _mixThread;
    private volatile bool _running;

    // Ayarlar (motor dışından atanır; ayrı alanlar → yarış yok)
    private volatile string? _outputDeviceId;
    private volatile string? _monitorDeviceId;
    private volatile string? _micDeviceId;
    private volatile bool _micEnabled = true;
    private volatile bool _monitorEnabled = true;
    private volatile bool _duckEnabled;
    private volatile float _micVolume = 1f;
    private volatile float _fxVolume = 1f;
    private volatile float _duckFactor = 0.25f;
    private volatile int _engineLatencyMs = 50;
    private float _duckCurrent = 1f;
    private int _engineLatency => _engineLatencyMs;

    // Durum
    private readonly Dictionary<Guid, PlaybackVoice> _voices = new();
    private int _activeFxCount;
    private double _cacheBytes;
    private readonly Dictionary<Guid, CachedSound> _cache = new();

    public event Action<string>? EngineError;        // kullanıcıya gösterilecek uyarı
    public event Action<string>? EngineInfo;
    public event Action<float, float>? Levels;       // (efekt piki, ana çıkış piki) 0..1

    // ---------------------------------------------------------------- kurulum

    public void ApplyConfiguration(string? outputDeviceId, string? micDeviceId, string? monitorDeviceId,
        bool micEnabled, bool monitorEnabled, bool duckEnabled,
        float micVolume, float fxVolume, float duckFactor, int engineLatencyMs)
    {
        bool needRestart;
        lock (_lock)
        {
            needRestart = _running && (
                outputDeviceId != _outputDeviceId ||
                micDeviceId != _micDeviceId ||
                monitorDeviceId != _monitorDeviceId ||
                engineLatencyMs != _engineLatencyMs);

            _outputDeviceId = outputDeviceId;
            _micDeviceId = micDeviceId;
            _monitorDeviceId = monitorDeviceId;
            _micEnabled = micEnabled;
            _monitorEnabled = monitorEnabled;
            _duckEnabled = duckEnabled;
            _micVolume = Math.Clamp(micVolume, 0f, 1.5f);
            _fxVolume = Math.Clamp(fxVolume, 0f, 1.5f);
            _duckFactor = Math.Clamp(duckFactor, 0f, 1f);
            _engineLatencyMs = Math.Clamp(engineLatencyMs, 20, 200);
        }
        if (!_running)
        {
            Start();
        }
        else if (needRestart)
        {
            Restart();
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_running) return;
            try
            {
                timeBeginPeriod(1); // 10 ms uyku hassasiyeti
            }
            catch { }

                _fxMixer = new MixingSampleProvider(Stereo48k) { ReadFully = false };
                _fxMixer.MixerInputEnded += OnMixerInputEnded;

            _bufMain = new BufferedWaveProvider(Stereo48k)
            {
                BufferDuration = TimeSpan.FromMilliseconds(600),
                DiscardOnBufferOverflow = true,
                ReadFully = true // tampon boşalırsa sessizlik ver → açılma/kopma yok
            };
            _bufMonitor = new BufferedWaveProvider(Stereo48k)
            {
                BufferDuration = TimeSpan.FromMilliseconds(600),
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };
            _bufMic = null;
            _micProvider = null;

            bool mainOk = TryOpenOutput();
            bool monOk = _monitorEnabled && TryOpenMonitor();
            TryOpenMic();

            // çıkış tamponunu önden doldur
            var silence = new float[SampleRate * Channels * PrimeMs / 1000];
            _bufMain?.AddSamples(ToBytes(silence), 0, silence.Length * sizeof(float));
            if (_outMonitor != null) _bufMonitor!.AddSamples(ToBytes(silence), 0, silence.Length * sizeof(float));

            _running = true;
            _mixThread = new Thread(MixLoop) { IsBackground = true, Name = "KayoneZ-Mixer", Priority = ThreadPriority.AboveNormal };
            _mixThread.Start();

            if (!mainOk)
                EngineError?.Invoke("Çıkış aygıtı açılamadı. Ses yönlendirme şu an kapalı — ayarları kontrol edin.");
            else
                EngineInfo?.Invoke("Ses motoru başlatıldı.");
        }
    }

    private bool TryOpenOutput()
    {
        try
        {
            MMDevice? device = null;
            try
            {
                device = _outputDeviceId == null
                    ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                    : _enumerator.GetDevice(_outputDeviceId);
            }
            catch { }
            if (device == null) { EngineError?.Invoke("Seçilen çıkış aygıtı bulunamadı."); return false; }

            _outMain = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, _engineLatency);
            _outMain.Init(_bufMain!);
            _outMain.Play();
            device.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            EngineError?.Invoke("Çıkış aygıtı başlatılamadı: " + ex.Message);
            _outMain = null;
            return false;
        }
    }

    private bool TryOpenMonitor()
    {
        try
        {
            MMDevice? device = null;
            try
            {
                device = _monitorDeviceId == null
                    ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                    : _enumerator.GetDevice(_monitorDeviceId);
            }
            catch { }
            if (device == null) return false;

            _outMonitor = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, _engineLatency);
            _outMonitor.Init(_bufMonitor!);
            _outMonitor.Play();
            device.Dispose();
            return true;
        }
        catch
        {
            _outMonitor = null;
            return false;
        }
    }

    private bool TryOpenMic()
    {
        if (_micDeviceId == null) return false;
        try
        {
            MMDevice device;
            try { device = _enumerator.GetDevice(_micDeviceId); }
            catch { EngineError?.Invoke("Seçilen mikrofon bulunamadı."); return false; }

            _bufMic = new BufferedWaveProvider(Stereo48k)
            {
                BufferDuration = TimeSpan.FromMilliseconds(400),
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };

            _micCapture = new WasapiCapture(device, true, 50)
            {
                WaveFormat = Stereo48k // paylaşımlı modda Windows yeniden örnekler
            };
            _micCapture.DataAvailable += (_, e) =>
            {
                var buf = _bufMic;
                if (buf != null) buf.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };
            _micCapture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null && _running)
                    EngineError?.Invoke("Mikrofon yakalama hatası: " + e.Exception.Message);
            };
            _micProvider = _bufMic.ToSampleProvider();
            _micCapture.StartRecording();
            device.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            EngineError?.Invoke("Mikrofon başlatılamadı: " + ex.Message);
            _micCapture = null;
            _micProvider = null;
            return false;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_running) return;
            _running = false;
            _mixThread?.Join(200);
            _mixThread = null;

            StopAll();
            SafeDispose(ref _outMain);
            SafeDispose(ref _outMonitor);
            if (_micCapture != null)
            {
                try { _micCapture.StopRecording(); _micCapture.Dispose(); } catch { }
                _micCapture = null;
            }
            _micProvider = null;
            _fxMixer = null;
            _bufMain = _bufMonitor = _bufMic = null;
            try { timeEndPeriod(1); } catch { }
        }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    // ---------------------------------------------------------------- çalma

    /// <summary>
    /// Ses çalar. Dönen voice durdurulabilir (voice.Stop() değil — StopVoice() kullanın).
    /// Retrigger davranışı çağıran taraf (UI/VM) iletişimle yönetir; motor yalnız ekler/kaldırır.
    /// </summary>
    public PlaybackVoice? Play(Guid soundId, ISampleProvider source, float volume)
    {
        PlaybackVoice? previous;
        PlaybackVoice? voice;
        lock (_lock)
        {
            if (_fxMixer == null) return null;
            previous = _voices.TryGetValue(soundId, out var old) ? old : null;
            var vol = new VolumeSampleProvider(source) { Volume = Math.Clamp(volume, 0f, 1.5f) };
            voice = new PlaybackVoice { SoundId = soundId, Source = source, Volume = vol, IsPlaying = true };
            _voices[soundId] = voice;
            voice.Stopped += v => { lock (_lock) { if (_voices.TryGetValue(soundId, out var cur) && cur == v) _voices.Remove(soundId); } };
            _fxMixer.AddMixerInput(vol);
            Interlocked.Increment(ref _activeFxCount);
        }
        if (previous != null)
            StopVoice(previous); // kilit dışında — StopVoice kendi kilidini alır
        return voice;
    }

    /// <summary>Voice'u kademeli olarak durdurur.</summary>
    public void StopVoice(PlaybackVoice voice)
    {
        lock (_lock)
        {
            if (!voice.IsPlaying) return;
            try { _fxMixer?.RemoveMixerInput(voice.Volume); } catch { }
            voice.IsPlaying = false;
            Interlocked.Decrement(ref _activeFxCount);
            if (_voices.TryGetValue(voice.SoundId, out var cur) && cur == voice)
                _voices.Remove(voice.SoundId);
        }
        voice.OnFinished();
    }

    public bool IsPlaying(Guid soundId)
    {
        lock (_lock) return _voices.ContainsKey(soundId);
    }

    public PlaybackVoice? GetVoice(Guid soundId)
    {
        lock (_lock) return _voices.TryGetValue(soundId, out var v) ? v : null;
    }

    public void StopAll()
    {
        List<PlaybackVoice> toStop;
        lock (_lock)
        {
            toStop = _voices.Values.ToList();
            _voices.Clear();
        }
        foreach (var v in toStop)
            StopVoice(v);
    }

    public int ActiveVoiceCount => Interlocked.CompareExchange(ref _activeFxCount, 0, 0);

    /// <summary>Kaynağı doğal olarak biten voice'u temizler (MixingSampleProvider girdiyi kendisi düşer).</summary>
    private void OnMixerInputEnded(object? sender, SampleProviderEventArgs e)
    {
        PlaybackVoice? finished = null;
        lock (_lock)
        {
            var entry = _voices.FirstOrDefault(kv => ReferenceEquals(kv.Value.Volume, e.SampleProvider));
            if (entry.Key != Guid.Empty)
            {
                finished = entry.Value;
                _voices.Remove(entry.Key);
                finished.IsPlaying = false;
                Interlocked.Decrement(ref _activeFxCount);
            }
        }
        finished?.OnFinished();
    }

    // ---------------------------------------------------------------- önbellek

    /// <summary>Sesi önbelleğe alır (düşük gecikmeli çalma için). Kapasite aşılırsa null.</summary>
    public CachedSound? TryCache(Guid soundId, string path, long maxBytes)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(soundId, out var cached)) return cached;
        }
        try
        {
            var cs = SoundDecoder.DecodeToCache(path, maxBytes);
            lock (_lock)
            {
                if (_cacheBytes + cs.Samples.Length * sizeof(float) > maxBytes) return null;
                _cache[soundId] = cs;
                _cacheBytes += cs.Samples.Length * sizeof(float);
            }
            return cs;
        }
        catch (CacheTooLargeException)
        {
            return null; // streaming ile çal
        }
        catch
        {
            return null; // decode hatası — çağıran streaming'i dener
        }
    }

    public void EvictCache(Guid soundId)
    {
        lock (_lock)
        {
            if (_cache.Remove(soundId, out var cs))
                _cacheBytes -= cs.Samples.Length * sizeof(float);
        }
    }

    public double CacheBytes => Interlocked.CompareExchange(ref _cacheBytes, 0, 0);

    // ---------------------------------------------------------------- karıştırma

    private float[] _fxBuf = new float[SampleRate * Channels * MixIntervalMs / 1000];
    private float[] _micBuf = new float[SampleRate * Channels * MixIntervalMs / 1000];
    private float[] _outBuf = new float[SampleRate * Channels * MixIntervalMs / 1000];
    private float[] _monBuf = new float[SampleRate * Channels * MixIntervalMs / 1000];
    private byte[] _outBytes = new byte[SampleRate * Channels * sizeof(float) * MixIntervalMs / 1000];

    private void MixLoop()
    {
        var sw = Stopwatch.StartNew();
        long nextTick = 0;
        var fxPeak = 0f;
        var outPeak = 0f;
        int meterDiv = 0;

        while (_running)
        {
            int frames = SampleRate * MixIntervalMs / 1000; // 480
            int fxSamples = 0;
            int micSamples = 0;

            // efektleri oku
            var fxMixer = _fxMixer;
            if (fxMixer != null)
            {
                try { fxSamples = fxMixer.Read(_fxBuf, 0, _fxBuf.Length); }
                catch { fxSamples = 0; }
            }

            // mikrofonu oku
            var micProvider = _micProvider;
            if (micProvider != null && _micEnabled)
            {
                try { micSamples = ReadStereo(micProvider, _micBuf, frames); }
                catch { micSamples = 0; }
            }

            // duck yumuşak geçişi
            float duckTarget = (_duckEnabled && fxSamples > 0) ? _duckFactor : 1f;
            _duckCurrent += (duckTarget - _duckCurrent) * 0.2f;
            float micGain = _micEnabled ? _micVolume * _duckCurrent : 0f;
            float fxGain = _fxVolume;

            // ana çıkışı karıştır + limiter
            fxPeak = 0f; outPeak = 0f;
            int framesFx = fxSamples / Channels;
            for (int i = 0; i < frames * Channels; i++)
            {
                float fx = i < fxSamples ? _fxBuf[i] * fxGain : 0f;
                float mic = i < micSamples ? _micBuf[i] * micGain : 0f;
                float mix = fx + mic;
                mix = Limit(mix);
                _outBuf[i] = mix;
                float a = Math.Abs(mix);
                if (a > outPeak) outPeak = a;
                if (i < fxSamples) { float f2 = Math.Abs(fx); if (f2 > fxPeak) fxPeak = f2; }
            }

            var bufMain = _bufMain;
            if (bufMain != null)
            {
                CopyToBytes(_outBuf, frames * Channels, _outBytes);
                bufMain.AddSamples(_outBytes, 0, frames * Channels * sizeof(float));
            }

            // monitör: yalnız efekt sesi
            var bufMon = _bufMonitor;
            if (_monitorEnabled && bufMon != null && _outMonitor != null)
            {
                for (int i = 0; i < frames * Channels; i++)
                    _monBuf[i] = i < fxSamples ? Limit(_fxBuf[i] * fxGain) : 0f;
                CopyToBytes(_monBuf, frames * Channels, _outBytes);
                bufMon.AddSamples(_outBytes, 0, frames * Channels * sizeof(float));
            }

            // seviye göstergesi ~5/sn
            if (++meterDiv >= 5)
            {
                meterDiv = 0;
                Levels?.Invoke(fxPeak, outPeak);
            }

            // 10 ms sabit adım
            nextTick += MixIntervalMs;
            long waitUs = nextTick * 1000 - sw.ElapsedTicks * 1000000L / Stopwatch.Frequency;
            if (waitUs > 0) Thread.Sleep((int)(waitUs / 1000));
        }
    }

    /// <summary>Basit yumuşak limiter — clipping önleme.</summary>
    internal static float Limit(float x)
    {
        if (x > 0.95f || x < -0.95f)
        {
            // yumuşak doygunluk: tanh benzeri
            const float knee = 0.05f;
            float sign = Math.Sign(x);
            float ax = Math.Abs(x);
            if (ax >= 1f) return sign * 0.999f;
            float t = (ax - 0.95f) / knee; // 0..1
            return sign * (0.95f + knee * (float)Math.Tanh(t) * 0.9f);
        }
        return x;
    }

    private static int ReadStereo(ISampleProvider provider, float[] dst, int frames)
    {
        // provider 48k/2ch değilse (mik format farkı) burada dönüştürme gerekir;
        // WasapiCapture WaveFormat'ı Stereo48k ayarlandığı için pratikte hep 2ch float gelir.
        if (provider.WaveFormat.Channels == 2 && provider.WaveFormat.SampleRate == SampleRate)
            return provider.Read(dst, 0, frames * Channels);
        // kanal uyuşmazlığı: mono → stereo çoğalt
        int read = provider.Read(dst, 0, frames); // mono kabul
        for (int i = read - 1; i >= 0; i--)
        {
            float v = dst[i];
            dst[i * 2] = v;
            dst[i * 2 + 1] = v;
        }
        return read * 2;
    }

    private static void CopyToBytes(float[] src, int sampleCount, byte[] dst)
    {
        Buffer.BlockCopy(src, 0, dst, 0, sampleCount * sizeof(float));
    }

    private static byte[] ToBytes(float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static void SafeDispose<T>(ref T? obj) where T : class, IDisposable
    {
        if (obj != null) { try { obj.Dispose(); } catch { } obj = null; }
    }

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint ms);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint ms);

    public void Dispose() => Stop();
}




