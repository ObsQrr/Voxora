using System.IO;
using NAudio.Wave;
using Xunit;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class VoiceChangerTests : IDisposable
{
    private readonly TempDir _dir = new();
    private static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    public void Dispose() => _dir.Dispose();

    private static float[] Sine(double frequency, int frames, float amplitude = 0.5f)
    {
        var samples = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            float value = (float)(Math.Sin(2 * Math.PI * frequency * i / 48000.0) * amplitude);
            samples[i * 2] = value;
            samples[i * 2 + 1] = value;
        }
        return samples;
    }

    private static float Rms(float[] data)
    {
        if (data.Length == 0) return 0;
        double sum = 0;
        foreach (var value in data) sum += value * value;
        return (float)Math.Sqrt(sum / data.Length);
    }

    [Fact]
    public void Settings_NormalizeClampsAllParameters()
    {
        var settings = new VoiceChangerSettings
        {
            PitchSemitones = 99,
            RobotMix = 2f,
            EchoMix = -1f,
            EchoDelayMs = 5000
        };

        settings.Normalize();

        Assert.Equal(12, settings.PitchSemitones);
        Assert.Equal(1f, settings.RobotMix);
        Assert.Equal(0f, settings.EchoMix);
        Assert.Equal(500, settings.EchoDelayMs);
    }

    [Theory]
    [InlineData(VoiceChangerPreset.Chipmunk, 12)]
    [InlineData(VoiceChangerPreset.Deep, -6)]
    [InlineData(VoiceChangerPreset.Natural, 0)]
    public void Presets_MapPitch(VoiceChangerPreset preset, int expectedPitch)
    {
        Assert.Equal(expectedPitch, VoiceChangerSettings.ForPreset(preset).PitchSemitones);
    }

    [Fact]
    public void Presets_MapRobotEchoAndTelephone()
    {
        Assert.True(VoiceChangerSettings.ForPreset(VoiceChangerPreset.Robot).RobotMix > 0);
        Assert.True(VoiceChangerSettings.ForPreset(VoiceChangerPreset.Echo).EchoMix > 0);
        Assert.True(VoiceChangerSettings.ForPreset(VoiceChangerPreset.Telephone).Telephone);
    }

    [Fact]
    public void Settings_ValueEquals_DetectsChanges()
    {
        var a = new VoiceChangerSettings { Enabled = true, PitchSemitones = 3, EchoMix = 0.2f };
        var b = a.Clone();
        Assert.True(a.ValueEquals(b));

        b.EchoMix = 0.3f;
        Assert.False(a.ValueEquals(b));
    }

    [Fact]
    public void RingMod_ChangesConstantSignal()
    {
        var source = new ArraySampleProvider(new[] { 0.5f, 0.5f }, Format, repeat: true);
        var ring = new RingModSampleProvider(source, 55, 1f);
        var buffer = new float[4800 * 2];

        int read = ring.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, read);
        Assert.True(buffer.Max() > 0.1f);
        Assert.True(buffer.Min() < -0.1f);
    }

    [Fact]
    public void Echo_ProducesDelayedImpulse()
    {
        var samples = new float[9600 * 2];
        samples[0] = 1f;
        samples[1] = 1f;
        var echo = new EchoSampleProvider(new ArraySampleProvider(samples, Format), 100, 1f);
        var buffer = new float[samples.Length];

        echo.Read(buffer, 0, buffer.Length);

        Assert.True(Math.Abs(buffer[4800 * 2]) > 0.01f,
            "100 ms gecikmede yankı sinyali bulunmalıydı.");
    }

    [Fact]
    public void TelephoneLowPass_AttenuatesHighFrequency()
    {
        var input = Sine(8000, 9600);
        var filter = new BiquadSampleProvider(new ArraySampleProvider(input, Format), BiquadKind.LowPass, 3400);
        var output = new float[input.Length];

        filter.Read(output, 0, output.Length);

        Assert.True(Rms(output) < Rms(input) * 0.5f,
            $"8 kHz telefon filtresinden geçmemeliydi. input={Rms(input):F4} output={Rms(output):F4}");
    }

    [Fact]
    public void BuildVoiceChain_DisabledReturnsSource()
    {
        var source = new ArraySampleProvider(new float[64], Format);
        var result = AudioEngine.BuildVoiceChain(source, new VoiceChangerSettings { Enabled = false });

        Assert.Same(source, result);
    }

    [Fact]
    public void BuildVoiceChain_EnabledWrapsSource()
    {
        var source = new ArraySampleProvider(new float[64], Format);
        var result = AudioEngine.BuildVoiceChain(source, new VoiceChangerSettings
        {
            Enabled = true,
            PitchSemitones = 3
        });

        Assert.NotSame(source, result);
        Assert.Equal(source.WaveFormat, result.WaveFormat);
    }

    [Fact]
    public void BuildVoiceChain_FullChain_CanBeRead()
    {
        var source = new ArraySampleProvider(Sine(440, 9600), Format);
        var chain = AudioEngine.BuildVoiceChain(source, new VoiceChangerSettings
        {
            Enabled = true,
            PitchSemitones = 4,
            RobotMix = 0.4f,
            EchoMix = 0.2f,
            EchoDelayMs = 120,
            Telephone = true
        });
        var buffer = new float[4800 * 2];

        int read = chain.Read(buffer, 0, buffer.Length);

        Assert.True(read > 0);
        Assert.All(buffer.Take(read), value => Assert.False(float.IsNaN(value) || float.IsInfinity(value)));
    }

    [Fact]
    public void VoiceChangerSettings_RoundTripThroughJson()
    {
        var path = Path.Combine(_dir.Path, "settings.json");
        var service = new SettingsService(path);
        service.Settings.VoiceChanger = new VoiceChangerSettings
        {
            Enabled = true,
            Preset = VoiceChangerPreset.Robot,
            PitchSemitones = 4,
            RobotMix = 0.6f,
            EchoMix = 0.2f,
            EchoDelayMs = 220,
            Telephone = true
        };
        service.Save();

        var reloaded = new SettingsService(path).Settings.VoiceChanger;

        Assert.True(reloaded.Enabled);
        Assert.Equal(VoiceChangerPreset.Robot, reloaded.Preset);
        Assert.Equal(4, reloaded.PitchSemitones);
        Assert.Equal(0.6f, reloaded.RobotMix, 3);
        Assert.Equal(0.2f, reloaded.EchoMix, 3);
        Assert.Equal(220, reloaded.EchoDelayMs);
        Assert.True(reloaded.Telephone);
    }

    private sealed class ArraySampleProvider : ISampleProvider
    {
        private readonly float[] _samples;
        private readonly bool _repeat;
        private int _position;

        public ArraySampleProvider(float[] samples, WaveFormat format, bool repeat = false)
        {
            _samples = samples;
            _repeat = repeat;
            WaveFormat = format;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_samples.Length == 0) return 0;
            int written = 0;
            while (written < count)
            {
                if (_position >= _samples.Length)
                {
                    if (!_repeat) break;
                    _position = 0;
                }
                int available = Math.Min(count - written, _samples.Length - _position);
                Array.Copy(_samples, _position, buffer, offset + written, available);
                _position += available;
                written += available;
            }
            return written;
        }
    }
}
