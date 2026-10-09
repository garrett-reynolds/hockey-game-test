using System;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Placeholder sounds made in code (noise and tones), so no sound files are needed yet.
    /// Swap these for real clips later; the cue names stay the same.
    /// </summary>
    public static class PlaceholderSounds
    {
        public const int Rate = 44100;

        public static AudioClip CrowdLoop() => Make("Crowd", Crowd(4f, 0.6f, 0.35f, false));
        public static AudioClip HeartbeatLoop() => Make("Heartbeat", Heartbeat());
        public static AudioClip Tick() => Make("Tick", Tone(0.04f, 1600f, 0.35f, 60f));
        public static AudioClip FastTick() => Make("Fast Tick", Tone(0.04f, 2100f, 0.4f, 60f));
        public static AudioClip Buzzer() => Make("Buzzer", Square(0.45f, 180f, 0.35f));
        public static AudioClip NetHit() => Make("Net Hit", NoiseBurst(0.25f, 0.5f, 0.15f, 25f, 3));
        public static AudioClip Horn() => Make("Goal Horn", Horn(1.6f));
        public static AudioClip PadThud() => Make("Pad Thud", Thud(0.35f));
        public static AudioClip BoardsClang() => Make("Boards Clang", Clang(0.9f));
        public static AudioClip Cheer() => Make("Cheer", Swell(Crowd(3f, 0.9f, 0.2f), 0.4f, 1.8f));
        public static AudioClip Groan() => Make("Groan", Swell(Crowd(1.8f, 0.7f, 0.65f), 0.25f, 1.0f));
        public static AudioClip NewBestCheer() => Make("New Best Cheer", Swell(Crowd(3.5f, 1f, 0.15f), 0.5f, 2.2f));

        static AudioClip Make(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static float[] Buffer(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];

        /// <summary>Crowd murmur: noise, smoothed by a one-pole low-pass. darkness 0 = bright, 1 = very dull.</summary>
        static float[] Crowd(float seconds, float gain, float darkness, bool fade = true)
        {
            var s = Buffer(seconds);
            var random = new System.Random(seconds.GetHashCode());
            float a = Mathf.Lerp(0.25f, 0.02f, darkness), y = 0f, slow = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float n = (float)(random.NextDouble() * 2 - 1);
                y += a * (n - y);
                slow += 0.0005f * ((float)random.NextDouble() - slow); // slow swell of the murmur
                s[i] = y * gain * (0.7f + 0.6f * slow) * 2f;
            }
            Normalise(s, gain);
            if (fade) FadeEdges(s, 0.05f); // the loop runs on without a dip
            return s;
        }

        /// <summary>Fade in over attack, then out until the end.</summary>
        static float[] Swell(float[] s, float attack, float sustain)
        {
            int attackN = (int)(attack * Rate), sustainN = (int)(sustain * Rate);
            for (int i = 0; i < s.Length; i++)
            {
                float env = i < attackN ? (float)i / attackN
                    : i < sustainN ? 1f
                    : 1f - (float)(i - sustainN) / Math.Max(1, s.Length - sustainN);
                s[i] *= env;
            }
            return s;
        }

        static float[] Heartbeat()
        {
            var s = Buffer(0.9f); // ~67 bpm loop: lub, dub, rest
            AddThump(s, 0f, 55f, 0.9f);
            AddThump(s, 0.22f, 48f, 0.65f);
            return s;
        }

        static void AddThump(float[] s, float at, float hz, float gain)
        {
            int start = (int)(at * Rate), length = (int)(0.16f * Rate);
            for (int i = 0; i < length && start + i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[start + i] += gain * Mathf.Sin(2 * Mathf.PI * hz * t) * Mathf.Exp(-t * 25f);
            }
        }

        static float[] Tone(float seconds, float hz, float gain, float decay)
        {
            var s = Buffer(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] = gain * Mathf.Sin(2 * Mathf.PI * hz * t) * Mathf.Exp(-t * decay);
            }
            return s;
        }

        static float[] Square(float seconds, float hz, float gain)
        {
            var s = Buffer(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] = gain * (Mathf.Repeat(t * hz, 1f) < 0.5f ? 1f : -1f);
            }
            FadeEdges(s, 0.01f);
            return s;
        }

        static float[] Horn(float seconds)
        {
            var s = Buffer(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float saw = 0f;
                for (int h = 1; h <= 6; h++) // a low, buzzy chord: 110 Hz and a fifth above
                    saw += (Mathf.Sin(2 * Mathf.PI * 110f * h * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 165f * h * t)) / h;
                s[i] = saw;
            }
            Normalise(s, 0.45f);
            FadeEdges(s, 0.08f);
            return s;
        }

        static float[] NoiseBurst(float seconds, float gain, float darkness, float decay, int seed)
        {
            var s = Buffer(seconds);
            var random = new System.Random(seed);
            float a = Mathf.Lerp(0.6f, 0.05f, darkness), y = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                y += a * ((float)(random.NextDouble() * 2 - 1) - y);
                s[i] = y * Mathf.Exp(-t * decay);
            }
            Normalise(s, gain);
            return s;
        }

        static float[] Thud(float seconds)
        {
            var s = NoiseBurst(seconds, 0.25f, 0.95f, 30f, 7);
            AddThump(s, 0f, 90f, 0.7f);
            Normalise(s, 0.7f);
            return s;
        }

        static float[] Clang(float seconds)
        {
            var s = Buffer(seconds);
            float[] partials = { 523f, 1187f, 1748f, 2633f, 3310f }; // inharmonic, metallic
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float v = 0f;
                for (int p = 0; p < partials.Length; p++)
                    v += Mathf.Sin(2 * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * (4f + 3f * p)) / (p + 1);
                s[i] = v;
            }
            var hit = NoiseBurst(0.05f, 1f, 0f, 80f, 11);
            for (int i = 0; i < hit.Length; i++) s[i] += 0.5f * hit[i];
            Normalise(s, 0.55f);
            return s;
        }

        static void Normalise(float[] s, float peak)
        {
            float max = 0f;
            foreach (float v in s) max = Mathf.Max(max, Mathf.Abs(v));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        static void FadeEdges(float[] s, float seconds)
        {
            int n = Mathf.Min((int)(seconds * Rate), s.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                s[i] *= k;
                s[s.Length - 1 - i] *= k;
            }
        }
    }
}
