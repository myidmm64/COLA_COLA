using System.Collections.Generic;
using ColaCola.Core;
using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Preloaded tactile cues and pressure-blended shake loops. Never changes rules or rule RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaTabletopAudio : MonoBehaviour
    {
        [Range(0, 1)] public float effectsVolume = .78f;
        private ShakeSelectionInteraction owner;
        private readonly Dictionary<string, AudioClip> clips = new();
        private readonly AudioSource[] voices = new AudioSource[12];
        private readonly AudioSource[] shake = new AudioSource[3];
        private GameObject root;
        private int nextVoice;
        private bool shaking, animated, gripped, setDown;
        private float pressure, shakeStarted, shakeDuration;
        private static readonly string[] Cues =
        {
            "Button", "Grip", "SetDown", "DeliverySlide", "DeliveryLand", "DeliveryLandGlass",
            "Blocked", "Cart", "MenuOpen", "MenuClose", "Tokens", "Safe", "NewBottle", "Turn", "Win", "Lose"
        };

        public static ColaTabletopAudio EnsureFor(ShakeSelectionInteraction interaction)
        {
            if (!interaction || !Application.IsPlaying(interaction.gameObject)) return null;
            var audio = interaction.GetComponent<ColaTabletopAudio>();
            if (!audio) audio = interaction.gameObject.AddComponent<ColaTabletopAudio>();
            audio.owner = interaction;
            audio.EnsureResources();
            return audio;
        }

        public static void Cue(ShakeSelectionInteraction interaction, string name, Vector3 position, float gain = .7f)
        {
            var audio = EnsureFor(interaction);
            if (audio && audio.isActiveAndEnabled) audio.Play(name, position, gain);
        }

        private AudioSource Source(string label, bool loop)
        {
            var child = new GameObject(label) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(root.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop;
            source.spatialBlend = .20f; source.dopplerLevel = 0;
            source.minDistance = 1.5f; source.maxDistance = 10;
            source.priority = 100; source.volume = 0;
            return source;
        }

        private void Load(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/SFX_" + name);
            clips[name] = clip;
            if (!clip) Debug.LogError("Missing tabletop audio: SFX_" + name, this);
        }

        private void EnsureResources()
        {
            if (root) return;
            root = new GameObject("Tabletop_SFX_Runtime") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            foreach (string name in Cues) Load(name);
            foreach (var definition in ColaItemCatalog.All) Load("Item_" + definition.Id);
            for (int i = 0; i < voices.Length; i++) voices[i] = Source("Tabletop cue " + i, false);
            var names = new[] { "Low", "Mid", "High" };
            for (int i = 0; i < shake.Length; i++)
            {
                string key = "Shake_" + names[i]; Load(key);
                shake[i] = Source("Bottle shake / " + names[i], true);
                shake[i].clip = clips[key];
            }
        }

        private void Play(string name, Vector3 position, float gain)
        {
            if (!clips.TryGetValue(name, out var clip) || !clip) return;
            AudioSource source = null;
            for (int i = 0; i < voices.Length; i++)
            {
                int index = (nextVoice + i) % voices.Length;
                if (voices[index].isPlaying) continue;
                source = voices[index]; nextVoice = (index + 1) % voices.Length; break;
            }
            // Bounded polyphony prevents repeated input from accumulating unbounded sources.
            if (!source) { source = voices[nextVoice]; nextVoice = (nextVoice + 1) % voices.Length; }
            source.Stop(); source.transform.position = position;
            source.clip = clip; source.pitch = 1;
            source.volume = effectsVolume * Mathf.Clamp01(gain);
            source.Play();
        }

        public void BeginShake(int strength, float pressureFraction, bool usesAnimator, float fallbackDuration)
        {
            EndShake();
            pressure = Mathf.Clamp01(pressureFraction);
            animated = usesAnimator; shaking = true; gripped = setDown = false;
            shakeStarted = Time.unscaledTime; shakeDuration = Mathf.Max(.1f, fallbackDuration);
            double start = AudioSettings.dspTime + .025;
            foreach (var source in shake)
            {
                source.volume = 0;
                source.pitch = Mathf.Lerp(.94f, 1.12f, (strength - 1) / 2f);
                if (source.clip) source.PlayScheduled(start);
            }
        }

        private void Update()
        {
            if (!shaking || !owner || !owner.bottle) return;
            float clock = animated ? (owner.shakeAnimator ? owner.shakeAnimator.PlaybackTime : 0) : Time.unscaledTime - shakeStarted;
            var burst = owner.burstPresentation;
            bool clear = !burst || burst.Stage == ColaBurstPresentation.BurstStage.Idle;
            float grip = animated ? ColaShakeAnimator.GripStartTime : 0;
            float release = animated ? ColaShakeAnimator.GripEndTime : shakeDuration;
            if (!gripped && clock >= grip && clear)
            { gripped = true; Play("Grip", owner.bottle.position, .45f); }
            if (!setDown && clock >= release && clear)
            { setDown = true; Play("SetDown", owner.bottle.position, .65f); }
            float begin = animated ? ColaShakeAnimator.ShakeStartTime : 0;
            float end = animated ? ColaShakeAnimator.ShakeEndTime : shakeDuration;
            float envelope = clear ? Mathf.SmoothStep(0, 1, (clock - begin) / .08f) *
                (1 - Mathf.SmoothStep(0, 1, (clock - (end - .09f)) / .09f)) : 0;
            if (animated && owner.shakeAnimator) envelope *= owner.shakeAnimator.ShakeStrokeWeight;
            // Equal-power crossfade at 0/50/100% actual pressure; strength changes the stroke pace.
            float blend = pressure * 2;
            for (int i = 0; i < shake.Length; i++)
            {
                shake[i].transform.position = owner.bottle.position;
                float weight = Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Abs(blend - i)));
                float target = effectsVolume * .65f * envelope * weight;
                shake[i].volume = Mathf.MoveTowards(shake[i].volume, target, Time.unscaledDeltaTime * 10);
            }
        }

        public void EndShake()
        {
            shaking = false;
            foreach (var source in shake) if (source) { source.Stop(); source.volume = 0; }
        }

        public void CancelAll()
        {
            EndShake();
            foreach (var voice in voices) if (voice) voice.Stop();
        }
        private void OnDisable() => CancelAll();
        private void OnDestroy() { CancelAll(); if (root) Destroy(root); }
    }
}
