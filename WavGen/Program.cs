// One-off WAV synthesizer for Garbus metronome samples.
// Method: short sine burst, 44100 Hz, 16-bit PCM, mono, linear-fade envelope.
// tick = 1200 Hz (80ms), downbeat = 1800 Hz (80ms, louder).

using System;
using System.IO;

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

    using var fs = new FileStream(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);

    int dataBytes = numSamples * 2;
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

string outDir = args.Length > 0 ? args[0] : ".";
WriteWav(Path.Combine(outDir, "metronome-tick.wav"), 1200.0, 0.7);
WriteWav(Path.Combine(outDir, "metronome-downbeat.wav"), 1800.0, 0.9);
Console.WriteLine("Done.");
