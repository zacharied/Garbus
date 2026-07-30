// One-off WAV synthesizer for the Garbus samples that are generated rather than recorded.
// All output is 44100 Hz, 16-bit PCM, mono.
//   metronome-tick / metronome-downbeat (-> Samples/Editor) — short sine burst, linear-fade envelope;
//     tick = 1200 Hz (80ms), downbeat = 1800 Hz (80ms, louder).
//   combo-break (-> Samples/Gameplay) — 450ms falling 300->90 Hz voice pile with a low-passed noise
//     thud at the onset, soft-clipped, peak-normalised.

using System;
using System.IO;

static void WriteSamples(string path, short[] samples)
{
    using var fs = new FileStream(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);

    int sampleRate = 44100;
    int dataBytes = samples.Length * 2;
    bw.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
    bw.Write(36 + dataBytes);
    bw.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
    bw.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
    bw.Write(16);
    bw.Write((short)1);
    bw.Write((short)1);
    bw.Write(sampleRate);
    bw.Write(sampleRate * 2);
    bw.Write((short)2);
    bw.Write((short)16);
    bw.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
    bw.Write(dataBytes);
    foreach (var s in samples) bw.Write(s);
}

static void WriteWav(string path, double frequency, double amplitude = 0.8, double durationSeconds = 0.08)
{
    int sampleRate = 44100;
    int numSamples = (int)(sampleRate * durationSeconds);
    short[] samples = new short[numSamples];

    for (int i = 0; i < numSamples; i++)
    {
        double t = (double)i / sampleRate;
        double envelope = 1.0 - (t / durationSeconds);
        double value = Math.Sin(2 * Math.PI * frequency * t) * envelope * short.MaxValue * amplitude;
        samples[i] = (short)Math.Clamp((int)value, short.MinValue, short.MaxValue);
    }

    WriteSamples(path, samples);
}

static void WriteComboBreak(string path)
{
    const int sample_rate = 44100;
    const double duration = 0.45;
    const double start_frequency = 300.0;
    const double end_frequency = 90.0;

    int numSamples = (int)(sample_rate * duration);
    double[] voice = new double[numSamples];

    // Deterministic noise source, so a regenerated asset is reproducible.
    long noiseState = 12345;
    double lowPassed = 0;

    double ratio = end_frequency / start_frequency;
    // Integral of the exponential frequency sweep — the instantaneous phase at time t.
    double phaseScale = 2 * Math.PI * start_frequency * duration / Math.Log(ratio);

    for (int i = 0; i < numSamples; i++)
    {
        double t = (double)i / sample_rate;
        double x = t / duration;
        double phase = phaseScale * (Math.Pow(ratio, x) - 1);

        // Two slightly detuned voices, a sub octave for weight, an odd harmonic for buzz.
        double v = 0.55 * Math.Sin(phase)
                   + 0.28 * Math.Sin(phase * 1.005 + 0.6)
                   + 0.34 * Math.Sin(phase * 0.5)
                   + 0.12 * Math.Sin(phase * 3.0);

        noiseState = (noiseState * 1103515245 + 12345) & 0x7FFFFFFF;
        double noise = noiseState / 1073741823.5 - 1.0;
        lowPassed += 0.12 * (noise - lowPassed);
        v += lowPassed * 2.2 * Math.Exp(-t / 0.035);

        // 4ms attack, exponential body decay, and a short tail fade so the end doesn't click.
        double envelope = Math.Min(1.0, t / 0.004)
                          * Math.Exp(-t / 0.14)
                          * Math.Min(1.0, (duration - t) / 0.02);

        voice[i] = Math.Tanh(v * envelope * 0.8 * 1.4);
    }

    double peak = 0;
    foreach (double v in voice) peak = Math.Max(peak, Math.Abs(v));

    double gain = 0.89 / peak;
    short[] samples = new short[numSamples];
    for (int i = 0; i < numSamples; i++)
        samples[i] = (short)(Math.Clamp(voice[i] * gain, -1.0, 1.0) * 32767);

    WriteSamples(path, samples);
}

string outDir = args.Length > 0 ? args[0] : ".";
WriteWav(Path.Combine(outDir, "metronome-tick.wav"), 1200.0, 0.7);
WriteWav(Path.Combine(outDir, "metronome-downbeat.wav"), 1800.0, 0.9);
WriteComboBreak(Path.Combine(outDir, "combo-break.wav"));
Console.WriteLine("Done.");
