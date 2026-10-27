using System;

namespace ColaCola.Core
{
    /// <summary>Unity random draws adapted to the model's exclusive-upper-bound probability contract.</summary>
    public static class ColaBurstRandom
    {
        private const int Steps = 1 << 24;

        public static double NextDouble()
        {
            // Random.value can include 1, which the model rejects. Integer Range excludes
            // its upper bound, so 100% remains certain and the draw is always in [0, 1).
            return UnityEngine.Random.Range(0, Steps) / (double)Steps;
        }

        public static Func<double> CreateSource(int? seed = null)
        {
#if UNITY_EDITOR
            if (seed.HasValue)
            {
                // Reproducible Editor probes also use Unity random. Preserve other systems'
                // global state while keeping a private replay state for this explicit seed.
                var saved = UnityEngine.Random.state;
                UnityEngine.Random.State replay;
                try { UnityEngine.Random.InitState(seed.Value); replay = UnityEngine.Random.state; }
                finally { UnityEngine.Random.state = saved; }
                return () =>
                {
                    var global = UnityEngine.Random.state;
                    try
                    {
                        UnityEngine.Random.state = replay;
                        double roll = NextDouble();
                        replay = UnityEngine.Random.state;
                        return roll;
                    }
                    finally { UnityEngine.Random.state = global; }
                };
            }
#endif
            return NextDouble;
        }
    }
}
