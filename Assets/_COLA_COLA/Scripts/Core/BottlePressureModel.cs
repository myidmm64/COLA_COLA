using System;

namespace ColaCola.Core
{
    public readonly struct ShakePrediction
    {
        public readonly int PressureBefore, Strength, PressureAfter, Limit;
        public int PressureIncrease => PressureAfter - PressureBefore;
        public readonly double BurstProbability;
        public ShakePrediction(int pressure, int strength, int limit, double probability)
            : this(pressure, strength, strength, limit, probability) { }

        public ShakePrediction(int pressure, int strength, int increase, int limit, double probability)
        {
            PressureBefore = pressure;
            Strength = strength;
            PressureAfter = checked(pressure + increase);
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

        public void ClearSelection() => SelectedStrength = 0;

        public void AdjustPressure(int delta)
        {
            if (HasBurst) throw new InvalidOperationException("A burst bottle cannot be adjusted.");
            Pressure = (int)Math.Max(0L, Math.Min(int.MaxValue - 5L, (long)Pressure + delta));
        }

        public ShakePrediction Predict(int strength, int increaseBonus = 0, double probabilityReduction = 0)
        {
            if (strength < 1 || strength > 3) throw new ArgumentOutOfRangeException(nameof(strength));
            if (double.IsNaN(probabilityReduction) || double.IsInfinity(probabilityReduction))
                throw new ArgumentOutOfRangeException(nameof(probabilityReduction));
            int increase = (int)Math.Max(0L, Math.Min(int.MaxValue - (long)Pressure, (long)strength + increaseBonus));
            int after = checked(Pressure + increase);
            double probability = after >= Limit ? 1d : Math.Max(0d, Math.Min(1d,
                Math.Pow((double)after / Limit, ProbabilityExponent) - Math.Max(0d, probabilityReduction)));
            return new ShakePrediction(Pressure, strength, increase, Limit, probability);
        }

        public bool TryCommit(Func<double> nextRandom, out ShakeResult result, int increaseBonus = 0, double probabilityReduction = 0)
        {
            result = default;
            if (SelectedStrength == 0 || HasBurst) return false;
            if (nextRandom == null) throw new ArgumentNullException(nameof(nextRandom));
            var prediction = Predict(SelectedStrength, increaseBonus, probabilityReduction);
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
