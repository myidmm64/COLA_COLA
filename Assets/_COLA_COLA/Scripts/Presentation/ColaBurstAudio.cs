using System;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Deterministic original PET crack, liquid spray and ringing tails. No rule RNG or downloaded audio.</summary>
    public static class ColaBurstAudio
    {
        public enum Sound { Strain = 1, Rupture = 2, Cap = 3 }

        public static AudioClip Build(Sound sound)
        {
            const int rate = 44100;
            float seconds = sound == Sound.Rupture ? 2.15f : sound == Sound.Strain ? .28f : .23f;
            var samples = new float[Mathf.CeilToInt(seconds * rate)];
            var noise = new System.Random(7319 + (int)sound);
            double filtered = 0, phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / rate, white = noise.NextDouble() * 2 - 1;
                filtered += (white - filtered) * .16;
                double value;
                if (sound == Sound.Rupture)
                {
                    // Fast plastic crack + downward pitched thump, followed by a sustained wet jet.
                    phase += 2 * Math.PI * (52 + 170 * Math.Exp(-t * 35)) / rate;
                    double crack = (white * .75 + Math.Sin(phase) * .7) * Math.Exp(-t * 65);
                    double body = Math.Sin(phase) * .62 * Math.Exp(-t * 13);
                    double jet = (white - filtered * .55) * .34 * (1 - Math.Exp(-t * 120)) * Math.Exp(-t * 2.8);
                    double sputter = filtered * .6 * Math.Pow(.5 + .5 * Math.Sin(t * 81), 4) * Math.Exp(-Math.Max(0, t - .35) * 3);
                    double ring = Math.Sin(2 * Math.PI * 2310 * t) * .025 * (1 - Math.Exp(-t * 40)) * Math.Exp(-t * 2.5);
                    value = crack + body + jet + sputter + ring;
                }
                else if (sound == Sound.Strain)
                {
                    double rise = t / seconds;
                    value = (filtered * .8 + Math.Sin(2 * Math.PI * (180 + rise * 160) * t) * .12) * rise * rise * .45;
                }
                else if (sound == Sound.Cap)
                    value = (Math.Sin(t * 2 * Math.PI * 1150) * .6 + white * .3) * Math.Exp(-t * 35);
                else
                    throw new ArgumentOutOfRangeException(nameof(sound));
                float fade = Mathf.Min(1, (samples.Length - i) / (rate * .025f));
                samples[i] = (float)Math.Tanh(value * 1.2) * fade * .85f;
            }
            var clip = AudioClip.Create("COLA_" + sound, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
