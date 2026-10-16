using System;

namespace ColaCola.Core
{
    public readonly struct ShakePrediction
    {
        public readonly int PressureBefore, Strength, PressureAfter, Limit;
        public readonly double BurstProbability;
        public ShakePrediction(int pressure, int strength, int limit, double probability)
        {
            PressureBefore = pressure;
            Strength = strength;
            PressureAfter = checked(pressure + strength);
            Limit = limit;
            BurstProbability = probability;
        }
    }

    public readonly struct ShakeResult
    {
        public readonly ShakePrediction Prediction;
        public readonly double Roll;
        public readonly bool Burst;
        public ShakeResult(ShakePrediction prediction, double roll)
        {
            Prediction = prediction;
            Roll = roll;
            Burst = roll < prediction.BurstProbability;
        }
    }

    /// <summary>Selection never changes pressure or consumes randomness; only Commit resolves a shake.</summary>
    public sealed class BottlePressureModel
    {
        public int Pressure { get; private set; }
        public int Limit { get; private set; }
        public int SelectedStrength { get; private set; }
        public bool HasBurst { get; private set; }
        public double ProbabilityExponent { get; }

        public BottlePressureModel(int limit, double exponent = 2, int pressure = 0)
        {
            if (double.IsNaN(exponent) || double.IsInfinity(exponent) || exponent <= 0)
                throw new ArgumentOutOfRangeException(nameof(exponent));
            ProbabilityExponent = exponent;
            BeginBottle(limit, pressure);
        }

        public void BeginBottle(int limit, int pressure = 0)
        {
            if (limit < 1 || limit > 999) throw new ArgumentOutOfRangeException(nameof(limit));
            if (pressure < 0 || pressure > int.MaxValue - 3) throw new ArgumentOutOfRangeException(nameof(pressure));
            Limit = limit;
            Pressure = pressure;
            SelectedStrength = 0;
            HasBurst = false;
        }

        public bool SelectStrength(int strength)
        {
            if (strength < 1 || strength > 3 || HasBurst) return false;
            SelectedStrength = strength;
            return true;
        }

        public ShakePrediction Predict(int strength)
        {
            if (strength < 1 || strength > 3) throw new ArgumentOutOfRangeException(nameof(strength));
            int after = checked(Pressure + strength);
            return new ShakePrediction(Pressure, strength, Limit,
                Math.Pow(Math.Min(1d, (double)after / Limit), ProbabilityExponent));
        }

        public bool TryCommit(Func<double> nextRandom, out ShakeResult result)
        {
            result = default;
            if (SelectedStrength == 0 || HasBurst) return false;
            if (nextRandom == null) throw new ArgumentNullException(nameof(nextRandom));
            var prediction = Predict(SelectedStrength);
            double roll = nextRandom();
            if (double.IsNaN(roll) || roll < 0 || roll >= 1)
                throw new ArgumentOutOfRangeException(nameof(nextRandom), "Random values must be in [0, 1).");
            result = new ShakeResult(prediction, roll);
            Pressure = prediction.PressureAfter;
            SelectedStrength = 0;
            HasBurst = result.Burst;
            return true;
        }
    }
}
