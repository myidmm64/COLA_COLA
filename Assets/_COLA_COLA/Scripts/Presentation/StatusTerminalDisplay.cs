using System;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>
    /// Drives the physical terminal's mesh digits and health segments. It contains no
    /// game rules: the initial values are scene samples, and only callers change them.
    /// Two health digits show 0..99 and four token digits show 0..9999; larger values
    /// saturate visually while the getters retain the actual requested integers.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class StatusTerminalDisplay : MonoBehaviour
    {
        [Serializable]
        public sealed class Panel
        {
            public Renderer[] healthSegments = Array.Empty<Renderer>();
            public MeshFilter[] currentHealthDigits = Array.Empty<MeshFilter>();
            public MeshFilter[] maxHealthDigits = Array.Empty<MeshFilter>();
            public MeshFilter[] tokenDigits = Array.Empty<MeshFilter>();
            public Renderer turnLamp;

            [SerializeField] private int health = 3;
            [SerializeField] private int maxHealth = 3;
            [SerializeField] private int tokens;

            [NonSerialized] internal double damageStartedAt = -1d;
            [NonSerialized] internal double tokensChangedAt = -1d;
            [NonSerialized] internal int damageLitSegments;
            [NonSerialized] internal MeshFilter[] cachedTokenFilters;
            [NonSerialized] internal Renderer[] cachedTokenRenderers;

            public int Health => health;
            public int MaxHealth => maxHealth;
            public int Tokens => tokens;
            public bool HasNumericOverflow => NeedsSaturation(health, currentHealthDigits)
                || NeedsSaturation(maxHealth, maxHealthDigits)
                || NeedsSaturation(tokens, tokenDigits);

            public Panel() { }

            public Panel(int initialHealth, int initialMaxHealth, int initialTokens)
            {
                SetValues(initialHealth, initialMaxHealth, initialTokens);
            }

            internal void SetValues(int newHealth, int newMaxHealth, int newTokens)
            {
                health = newHealth;
                maxHealth = newMaxHealth;
                tokens = newTokens;
            }
        }

        public Panel boss = new Panel(3, 3, 12);
        public Panel player = new Panel(3, 3, 15);
        public Mesh[] digitMeshes = new Mesh[10];
        public Material healthOnMaterial;
        public Material healthOffMaterial;
        public Material tokenMaterial;
        public Material damageMaterial;

        [SerializeField] private bool playerTurn = true;

        private const double DamageSeconds = 0.42d;
        private const double TokenTickSeconds = 0.22d;
        private bool refreshRequested = true;
        private bool wasAnimating;

        public int BossHealth => boss?.Health ?? 0;
        public int BossMaxHealth => boss?.MaxHealth ?? 0;
        public int BossTokens => boss?.Tokens ?? 0;
        public int PlayerHealth => player?.Health ?? 0;
        public int PlayerMaxHealth => player?.MaxHealth ?? 0;
        public int PlayerTokens => player?.Tokens ?? 0;
        public bool IsPlayerTurn => playerTurn;
        public bool IsAnimating => Application.IsPlaying(gameObject) && isActiveAndEnabled
            && (HasAnimation(boss, ColaGameClock.TimeAsDouble)
                || HasAnimation(player, ColaGameClock.TimeAsDouble));

        public void SetBossState(int health, int maxHealth, int tokens, bool animate = true)
        {
            if (boss == null)
                boss = new Panel();

            SetState(boss, health, maxHealth, tokens, animate);
        }

        public void SetPlayerState(int health, int maxHealth, int tokens, bool animate = true)
        {
            if (player == null)
                player = new Panel();

            SetState(player, health, maxHealth, tokens, animate);
        }

        public void SetActiveParticipant(bool isPlayerTurn)
        {
            playerTurn = isPlayerTurn;
            RefreshDisplay();
        }

        /// <summary>Updates edit-mode bindings after a model or scene factory fills them.</summary>
        public void RefreshDisplay()
        {
            double now = Application.IsPlaying(gameObject) ? ColaGameClock.TimeAsDouble : -1d;
            RenderPanel(boss, !playerTurn, now);
            RenderPanel(player, playerTurn, now);
            refreshRequested = false;
        }

        /// <summary>Checks the physical bindings without requiring a running match.</summary>
        public bool TryValidateBindings(out string message)
        {
            if (digitMeshes == null || digitMeshes.Length < 10)
            {
                message = "The display requires meshes for digits 0 through 9.";
                return false;
            }

            for (int i = 0; i < 10; i++)
            {
                if (digitMeshes[i] == null)
                {
                    message = "Digit mesh " + i + " is missing.";
                    return false;
                }
            }

            if (healthOnMaterial == null || healthOffMaterial == null
                || tokenMaterial == null || damageMaterial == null)
            {
                message = "One or more display materials are missing.";
                return false;
            }

            if (!ValidatePanel(boss) || !ValidatePanel(player))
            {
                message = "Both panels need health segments, two current/max health digits, and four token digits.";
                return false;
            }

            message = "Both physical panels and all ten digit meshes are bound.";
            return true;
        }

        private void OnEnable()
        {
            CancelAnimation(boss);
            CancelAnimation(player);
            wasAnimating = false;
            RefreshDisplay();
        }

        private void OnDisable()
        {
            // No delayed callbacks or coroutines survive a disable or scene restart.
            CancelAnimation(boss);
            CancelAnimation(player);
            wasAnimating = false;
            RefreshDisplay();
        }

        private void OnValidate()
        {
            // OnValidate can run during deserialization; renderer writes wait for Update.
            refreshRequested = true;
        }

        private void Update()
        {
            bool animating = IsAnimating;
            if (refreshRequested || animating || wasAnimating)
                RefreshDisplay();

            wasAnimating = animating;
        }

        private void SetState(Panel panel, int health, int maxHealth, int tokens, bool animate)
        {
            int previousFill = FilledSegments(panel);
            bool tookDamage = health < panel.Health;
            bool tokensChanged = tokens != panel.Tokens;
            panel.SetValues(health, maxHealth, tokens);

            if (animate && Application.IsPlaying(gameObject) && isActiveAndEnabled)
            {
                double now = ColaGameClock.TimeAsDouble;
                if (tookDamage)
                {
                    panel.damageStartedAt = now;
                    panel.damageLitSegments = Math.Max(previousFill, FilledSegments(panel));
                }

                if (tokensChanged)
                    panel.tokensChangedAt = now;
            }
            else
            {
                CancelAnimation(panel);
            }

            RefreshDisplay();
            wasAnimating = IsAnimating;
        }

        private void RenderPanel(Panel panel, bool activeTurn, double now)
        {
            if (panel == null)
                return;

            int filled = FilledSegments(panel);
            bool damageFlash = IsWithin(now, panel.damageStartedAt, DamageSeconds)
                && ((int)((now - panel.damageStartedAt) / 0.07d) & 1) == 0;

            if (panel.healthSegments != null)
            {
                for (int i = 0; i < panel.healthSegments.Length; i++)
                {
                    Material material = i < filled ? healthOnMaterial : healthOffMaterial;
                    if (damageFlash && i < panel.damageLitSegments && damageMaterial != null)
                        material = damageMaterial;

                    SetMaterial(panel.healthSegments[i], material);
                }
            }

            WriteNumber(panel.currentHealthDigits, panel.Health);
            WriteNumber(panel.maxHealthDigits, panel.MaxHealth);
            WriteNumber(panel.tokenDigits, panel.Tokens);
            EnsureTokenRendererCache(panel);

            bool tokenDim = IsWithin(now, panel.tokensChangedAt, TokenTickSeconds)
                && ((int)((now - panel.tokensChangedAt) / 0.055d) & 1) == 0;
            Material tokenDisplayMaterial = tokenDim ? healthOffMaterial : tokenMaterial;
            if (panel.cachedTokenRenderers != null)
            {
                for (int i = 0; i < panel.cachedTokenRenderers.Length; i++)
                    SetMaterial(panel.cachedTokenRenderers[i], tokenDisplayMaterial);
            }

            SetMaterial(panel.turnLamp, activeTurn ? healthOnMaterial : healthOffMaterial);
        }

        private void WriteNumber(MeshFilter[] filters, int value)
        {
            if (filters == null)
                return;

            int remaining = Math.Max(0, Math.Min(value, DisplayLimit(filters.Length)));
            for (int i = filters.Length - 1; i >= 0; i--)
            {
                int digit = remaining % 10;
                remaining /= 10;
                if (filters[i] == null)
                    continue;

                Mesh mesh = digitMeshes != null && digit < digitMeshes.Length ? digitMeshes[digit] : null;
                if (filters[i].sharedMesh != mesh)
                    filters[i].sharedMesh = mesh;
            }
        }

        private static int FilledSegments(Panel panel)
        {
            if (panel == null || panel.healthSegments == null || panel.MaxHealth <= 0 || panel.Health <= 0)
                return 0;

            double ratio = Math.Min(1d, (double)panel.Health / panel.MaxHealth);
            return (int)Math.Ceiling(ratio * panel.healthSegments.Length);
        }

        private static void EnsureTokenRendererCache(Panel panel)
        {
            int count = panel.tokenDigits?.Length ?? 0;
            if (panel.cachedTokenFilters == null || panel.cachedTokenFilters.Length != count)
            {
                panel.cachedTokenFilters = new MeshFilter[count];
                panel.cachedTokenRenderers = new Renderer[count];
            }

            for (int i = 0; i < count; i++)
            {
                MeshFilter filter = panel.tokenDigits[i];
                if (panel.cachedTokenFilters[i] == filter)
                    continue;

                panel.cachedTokenFilters[i] = filter;
                panel.cachedTokenRenderers[i] = filter != null ? filter.GetComponent<Renderer>() : null;
            }
        }

        private static bool HasAnimation(Panel panel, double now)
        {
            return panel != null && (IsWithin(now, panel.damageStartedAt, DamageSeconds)
                || IsWithin(now, panel.tokensChangedAt, TokenTickSeconds));
        }

        private static bool IsWithin(double now, double startedAt, double duration)
        {
            return startedAt >= 0d && now >= startedAt && now - startedAt < duration;
        }

        private static void CancelAnimation(Panel panel)
        {
            if (panel == null)
                return;

            panel.damageStartedAt = -1d;
            panel.tokensChangedAt = -1d;
            panel.damageLitSegments = 0;
        }

        private static void SetMaterial(Renderer renderer, Material material)
        {
            if (renderer != null && material != null && renderer.sharedMaterial != material)
                renderer.sharedMaterial = material;
        }

        private static bool ValidatePanel(Panel panel)
        {
            return panel != null && AllAssigned(panel.healthSegments, 1)
                && AllAssigned(panel.currentHealthDigits, 2)
                && AllAssigned(panel.maxHealthDigits, 2)
                && AllAssigned(panel.tokenDigits, 4);
        }

        private static bool AllAssigned<T>(T[] objects, int minimum) where T : UnityEngine.Object
        {
            if (objects == null || objects.Length < minimum)
                return false;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null)
                    return false;
            }

            return true;
        }

        private static bool NeedsSaturation(int value, MeshFilter[] filters)
        {
            return value < 0 || value > DisplayLimit(filters?.Length ?? 0);
        }

        private static int DisplayLimit(int digits)
        {
            if (digits <= 0)
                return 0;
            if (digits >= 10)
                return int.MaxValue;

            int limit = 0;
            for (int i = 0; i < digits; i++)
                limit = limit * 10 + 9;

            return limit;
        }
    }
}
