using System;

namespace LeaderboardPlus;

public static class Synth
{
    public const int rate = 44100;
    public const int tick = 0, tap = 1, swoosh = 2, rise = 3, confirm = 4, thud = 5, pluck = 6, down = 7, n1 = 8;
    public const int count = 12;
    static readonly float[] steps = { 523.25f, 659.25f, 783.99f, 1046.5f };

    static float[] Buf(float len) => new float[(int)(len * rate)];

    static void Add(float[] buf, float at, float f0, float f1, float len, float gain, float decay)
    {
        int n = (int)(len * rate), off = (int)(at * rate);
        double phase = 0;
        for (var i = 0; i < n && i + off < buf.Length; i++)
        {
            float t = i / (float)rate, k = i / (float)n;
            phase += 2 * Math.PI * (f0 + (f1 - f0) * k) / rate;
            var env = MathF.Min(1f, t / 0.003f) * MathF.Exp(-decay * t) * MathF.Min(1f, (n - i) / (0.005f * rate));
            buf[i + off] += (float)Math.Sin(phase) * env * gain;
        }
    }

    static void Noise(float[] buf, float len, float gain, float a0, float a1, int seed)
    {
        var n = (int)(len * rate);
        var rng = new Random(seed);
        float lp = 0f, slow = 0f;
        for (var i = 0; i < n && i < buf.Length; i++)
        {
            float k = i / (float)n, x = (float)rng.NextDouble() * 2f - 1f;
            lp += (x - lp) * (a0 + (a1 - a0) * k);
            slow += (lp - slow) * 0.03f;
            var env = MathF.Sin(MathF.PI * k);
            buf[i] += (lp - slow) * env * env * gain;
        }
    }

    static void Echo(float[] buf, float delay, float feedback)
    {
        var k = (int)(delay * rate);
        for (var i = k; i < buf.Length; i++) buf[i] += buf[i - k] * feedback;
    }

    static void Pluck(float[] buf, float at, float f, float gain)
    {
        Add(buf, at, f, f, 0.4f, gain, 14f);
        Add(buf, at, f * 4f, f * 4f, 0.12f, gain * 0.22f, 45f);
        Add(buf, at, f * 10f, f * 10f, 0.05f, gain * 0.07f, 90f);
    }

    static void Bell(float[] buf, float at, float f, float gain)
    {
        Add(buf, at, f, f, 0.8f, gain, 5.5f);
        Add(buf, at, f * 2f, f * 2f, 0.6f, gain * 0.3f, 8f);
        Add(buf, at, f * 3.01f, f * 3.01f, 0.4f, gain * 0.12f, 13f);
        Add(buf, at, f * 0.5f, f * 0.5f, 0.5f, gain * 0.15f, 7f);
    }

    static float[] Raw(int kind)
    {
        float[] buf;
        switch (kind)
        {
            case tick:
                buf = Buf(0.07f);
                Add(buf, 0f, 2600f, 2600f, 0.04f, 0.2f, 110f);
                Add(buf, 0f, 1300f, 1300f, 0.06f, 0.16f, 80f);
                return buf;
            case tap:
                buf = Buf(0.12f);
                Add(buf, 0f, 560f, 300f, 0.1f, 0.7f, 38f);
                Add(buf, 0f, 1120f, 600f, 0.05f, 0.15f, 70f);
                Noise(buf, 0.006f, 0.25f, 0.6f, 0.3f, 3);
                return buf;
            case swoosh:
                buf = Buf(0.26f);
                Noise(buf, 0.25f, 0.9f, 0.06f, 0.45f, 7);
                return buf;
            case rise:
                buf = Buf(0.7f);
                Pluck(buf, 0f, 523.25f, 0.4f);
                Pluck(buf, 0.07f, 783.99f, 0.4f);
                Echo(buf, 0.11f, 0.25f);
                return buf;
            case confirm:
                buf = Buf(1.1f);
                Bell(buf, 0f, 1046.5f, 0.4f);
                Bell(buf, 0.075f, 1318.5f, 0.4f);
                Bell(buf, 0.15f, 1568f, 0.45f);
                Echo(buf, 0.13f, 0.28f);
                return buf;
            case thud:
                buf = Buf(0.32f);
                Add(buf, 0f, 130f, 52f, 0.3f, 0.9f, 16f);
                Add(buf, 0f, 65f, 40f, 0.3f, 0.4f, 12f);
                Noise(buf, 0.012f, 0.3f, 0.3f, 0.1f, 5);
                return buf;
            case pluck:
                return Bar(880f, 0.5f);
            case down:
                return Bar(587.33f, 0.45f);
            default:
                return Bar(steps[Math.Min(3, kind - n1)], 0.5f);
        }
    }

    static float[] Bar(float f, float gain)
    {
        var buf = Buf(0.42f);
        Pluck(buf, 0f, f, gain);
        return buf;
    }

    public static float[] Make(int kind)
    {
        var buf = Raw(kind);
        var peak = 0f;
        foreach (var sample in buf) peak = MathF.Max(peak, MathF.Abs(sample));
        var scale = peak > 0.9f ? 0.9f / peak : 1f;
        var fade = (int)(0.005f * rate);
        for (var i = 0; i < buf.Length; i++) buf[i] *= scale * MathF.Min(1f, (buf.Length - i) / (float)fade);
        return buf;
    }
}
