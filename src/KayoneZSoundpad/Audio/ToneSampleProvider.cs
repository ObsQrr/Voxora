using NAudio.Wave;

namespace KayoneZSoundpad.Audio;

/// <summary>
/// Kısa, ölçülü bir test tonu üretir. Yönlendirme testinde gerçek miks hattına
/// girer; böylece kullanıcı sesin gerçekten seçili çıkışa gittiğini duyar.
/// </summary>
internal sealed class ToneSampleProvider : ISampleProvider
{
    private readonly WaveFormat _format;
    private readonly double _phaseStep;
    private readonly int _totalFrames;
    private readonly int _fadeFrames;
    private double _phase;
    private int _frame;

    public ToneSampleProvider(WaveFormat format, double frequencyHz, double seconds, float amplitude = 0.22f)
    {
        _format = format;
        Amplitude = amplitude;
        _phaseStep = 2 * Math.PI * frequencyHz / format.SampleRate;
        _totalFrames = Math.Max(1, (int)(format.SampleRate * seconds));
        _fadeFrames = Math.Max(1, format.SampleRate / 50); // 20 ms
    }

    public float Amplitude { get; }

    public WaveFormat WaveFormat => _format;

    public int Read(float[] buffer, int offset, int count)
    {
        int channels = _format.Channels;
        int framesWanted = count / channels;
        int remaining = _totalFrames - _frame;
        if (remaining <= 0) return 0;

        int frames = Math.Min(framesWanted, remaining);
        for (int i = 0; i < frames; i++)
        {
            double envelope = 1.0;
            if (_frame < _fadeFrames) envelope = _frame / (double)_fadeFrames;
            else if (remaining - i <= _fadeFrames) envelope = (remaining - i) / (double)_fadeFrames;

            float value = (float)(Math.Sin(_phase) * Amplitude * envelope);
            _phase += _phaseStep;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;

            int index = offset + i * channels;
            for (int ch = 0; ch < channels; ch++) buffer[index + ch] = value;
            _frame++;
        }

        return frames * channels;
    }
}
