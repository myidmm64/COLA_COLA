using System;

namespace ColaCola.Core
{
    public readonly struct ShakePrediction
    {
        public readonly int PressureBefore, Strength, PressureAfter, Limit;
        public int PressureIncrease => PressureAfter - PressureBefore;
        public readonly double BurstProbability;
        public ShakePrediction(int pressure, int strength, int limit, double probability)
            : this(pressure, strength, BottlePressureModel.PressureIncreaseFor(strength), limit, probability) { }

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
        public const int LowPressure = 2;
        public const int MediumPressure = 4;
        public const int HighPressure = 8;
        public static int PressureIncreaseFor(int strength) => strength switch
        {
            1 => LowPressure, 2 => MediumPressure, 3 => HighPressure,
            _ => throw new ArgumentOutOfRangeException(nameof(strength))
        };
        public int Pressure { get; private set; }
        public int Limit { get; private set; }
        public int SelectedStrength { get; private set; }
        public bool HasBurst { get; private set; }
        public bool ForceBurst { get; set; }
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
            if (pressure < 0 || pressure > int.MaxValue - HighPressure) throw new ArgumentOutOfRangeException(nameof(pressure));
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

        public void AdjustLimit(int delta)
        {
            if (HasBurst) throw new InvalidOperationException("A burst bottle cannot be adjusted.");
            Limit = (int)Math.Max(1L, Math.Min(999L, (long)Limit + delta));
        }

        public void AdjustPressure(int delta)
        {
            if (HasBurst) throw new InvalidOperationException("A burst bottle cannot be adjusted.");
            Pressure = (int)Math.Max(0L, Math.Min(int.MaxValue - (long)HighPressure, (long)Pressure + delta));
        }

        public ShakePrediction Predict(int strength, int increaseBonus = 0, double probabilityReduction = 0, int pressureMultiplier = 1, double probabilityIncrease = 0)
            => PredictAtPressure(Pressure, strength, increaseBonus, probabilityReduction, pressureMultiplier, probabilityIncrease);

        public ShakePrediction PredictAtPressure(int pressure, int strength, int increaseBonus = 0, double probabilityReduction = 0, int pressureMultiplier = 1, double probabilityIncrease = 0)
        {
            if (strength < 1 || strength > 3) throw new ArgumentOutOfRangeException(nameof(strength));
            if (pressure < 0) throw new ArgumentOutOfRangeException(nameof(pressure));
            if (double.IsNaN(probabilityReduction) || double.IsInfinity(probabilityReduction))
                throw new ArgumentOutOfRangeException(nameof(probabilityReduction));
            if (double.IsNaN(probabilityIncrease) || double.IsInfinity(probabilityIncrease))
                throw new ArgumentOutOfRangeException(nameof(probabilityIncrease));
            int increase = (int)Math.Min(int.MaxValue - (long)pressure, Math.Max(0L, ((long)PressureIncreaseFor(strength) + increaseBonus) * Math.Max(1, pressureMultiplier)));
            int after = checked(pressure + increase);
            // Restore the pressure-based per-shake curve. Preview and Commit share this path.
            double baseProbability = Math.Pow((double)after / Limit, ProbabilityExponent);
            double probability = ForceBurst || after >= Limit ? 1d : Math.Max(0d, Math.Min(1d,
                baseProbability - Math.Max(0d, probabilityReduction) + Math.Max(0d, probabilityIncrease)));
            return new ShakePrediction(pressure, strength, increase, Limit, probability);
        }

        public bool TryCommit(Func<double> nextRandom, out ShakeResult result, int increaseBonus = 0, double probabilityReduction = 0, int pressureMultiplier = 1, double probabilityIncrease = 0)
        {
            result = default;
            if (SelectedStrength == 0 || HasBurst) return false;
            if (nextRandom == null) throw new ArgumentNullException(nameof(nextRandom));
            var prediction = Predict(SelectedStrength, increaseBonus, probabilityReduction, pressureMultiplier, probabilityIncrease);
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
