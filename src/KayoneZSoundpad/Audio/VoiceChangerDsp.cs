using NAudio.Wave;

namespace KayoneZSoundpad.Audio;

/// <summary>Ring modülasyonu (robot sesi).</summary>
public sealed class RingModSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _mix;
    private readonly double _phaseStep;
    private double _phase;

    public RingModSampleProvider(ISampleProvider source, float frequencyHz, float mix)
    {
        _source = source;
        _mix = Math.Clamp(mix, 0f, 1f);
        _phaseStep = 2 * Math.PI * frequencyHz / source.WaveFormat.SampleRate;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        int channels = WaveFormat.Channels;
        for (int frame = 0; frame < read / channels; frame++)
        {
            float carrier = (float)Math.Sin(_phase);
            _phase += _phaseStep;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;

            int baseIndex = offset + frame * channels;
            for (int ch = 0; ch < channels; ch++)
            {
                float dry = buffer[baseIndex + ch];
                buffer[baseIndex + ch] = dry * (1f - _mix) + dry * carrier * _mix;
            }
        }
        return read;
    }
}

/// <summary>Geri beslemeli gecikme hattı (echo).</summary>
public sealed class EchoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _mix;
    private readonly float _feedback;
    private readonly float[] _delay;
    private readonly int _channels;
    private int _index;

    public EchoSampleProvider(ISampleProvider source, int delayMs, float mix, float feedback = 0.35f)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        _mix = Math.Clamp(mix, 0f, 1f);
        _feedback = Math.Clamp(feedback, 0f, 0.85f);
        int frames = Math.Max(1, source.WaveFormat.SampleRate * Math.Clamp(delayMs, 50, 500) / 1000);
        _delay = new float[frames * _channels];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        int frames = read / _channels;
        int delayFrames = _delay.Length / _channels;

        for (int frame = 0; frame < frames; frame++)
        {
            int sourceIndex = offset + frame * _channels;
            int delayIndex = _index * _channels;
            for (int ch = 0; ch < _channels; ch++)
            {
                float input = buffer[sourceIndex + ch];
                float delayed = _delay[delayIndex + ch];
                _delay[delayIndex + ch] = input + delayed * _feedback;
                buffer[sourceIndex + ch] = input * (1f - _mix) + delayed * _mix;
            }
            _index++;
            if (_index >= delayFrames) _index = 0;
        }
        return read;
    }
}

public enum BiquadKind
{
    HighPass,
    LowPass
}

/// <summary>Telefon bandını oluşturmak için basit RBJ biquad filtresi.</summary>
public sealed class BiquadSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly float _b0, _b1, _b2, _a1, _a2;
    private readonly float[] _x1, _x2, _y1, _y2;

    public BiquadSampleProvider(ISampleProvider source, BiquadKind kind, float frequencyHz, float q = 0.707f)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        _x1 = new float[_channels];
        _x2 = new float[_channels];
        _y1 = new float[_channels];
        _y2 = new float[_channels];

        double sampleRate = source.WaveFormat.SampleRate;
        double w0 = 2 * Math.PI * frequencyHz / sampleRate;
        double cos = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2 * Math.Max(0.1f, q));

        double b0, b1, b2;
        if (kind == BiquadKind.HighPass)
        {
            b0 = (1 + cos) / 2;
            b1 = -(1 + cos);
            b2 = (1 + cos) / 2;
        }
        else
        {
            b0 = (1 - cos) / 2;
            b1 = 1 - cos;
            b2 = (1 - cos) / 2;
        }
        double a0 = 1 + alpha;
        double a1 = -2 * cos;
        double a2 = 1 - alpha;

        _b0 = (float)(b0 / a0);
        _b1 = (float)(b1 / a0);
        _b2 = (float)(b2 / a0);
        _a1 = (float)(a1 / a0);
        _a2 = (float)(a2 / a0);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        int frames = read / _channels;
        for (int frame = 0; frame < frames; frame++)
        {
            int index = offset + frame * _channels;
            for (int ch = 0; ch < _channels; ch++)
            {
                float x = buffer[index + ch];
                float y = _b0 * x + _b1 * _x1[ch] + _b2 * _x2[ch] - _a1 * _y1[ch] - _a2 * _y2[ch];
                _x2[ch] = _x1[ch];
                _x1[ch] = x;
                _y2[ch] = _y1[ch];
                _y1[ch] = y;
                buffer[index + ch] = y;
            }
        }
        return read;
    }
}
