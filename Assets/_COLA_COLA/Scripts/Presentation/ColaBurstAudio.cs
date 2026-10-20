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
            float seconds = sound == Sound.Rupture ? 2.4f : sound == Sound.Strain ? .30f : .23f;
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
                    // Broadband detonation crack, deep downward boom and a rolling pressure tail.
                    phase += 2 * Math.PI * (32 + 185 * Math.Exp(-t * 33)) / rate;
                    double crack = (white * 1.7 + Math.Sin(phase) * .8) * Math.Exp(-t * 95);
                    double body = Math.Sin(phase) * 1.15 * Math.Exp(-t * 5.6);
                    double chest = Math.Sin(2 * Math.PI * 48 * t) * .65 * (1 - Math.Exp(-t * 150)) * Math.Exp(-t * 8);
                    double rumble = filtered * 1.2 * (1 - Math.Exp(-t * 160)) * Math.Exp(-t * 3.8);
                    double jet = (white - filtered * .55) * .36 * (1 - Math.Exp(-t * 120)) * Math.Exp(-t * 3.1);
                    double sputter = filtered * .5 * Math.Pow(.5 + .5 * Math.Sin(t * 81), 4) * Math.Exp(-Math.Max(0, t - .35) * 3);
                    double ring = Math.Sin(2 * Math.PI * 1730 * t) * .035 * (1 - Math.Exp(-t * 70)) * Math.Exp(-t * 2.2);
                    // Low, short room reflections come from already generated samples; no live audio allocations.
                    double reflections = (i >= 1544 ? samples[i - 1544] * .20 : 0) +
                        (i >= 3396 ? samples[i - 3396] * .11 : 0);
                    value = crack + body + chest + rumble + jet + sputter + ring + reflections;
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
