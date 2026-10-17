using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Two synchronized original music stems; pressure changes the mix without restarting the song.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaDuelMusic : MonoBehaviour
    {
        [Range(0, 1)] public float musicVolume = .34f;
        public AudioSource Ambience { get; private set; }
        public AudioSource Tension { get; private set; }
        public float Intensity { get; private set; }
        public float Duck { get; private set; } = 1;
        private ColaDuelController match;

        public static ColaDuelMusic EnsureFor(ColaDuelController duel)
        {
            var music = duel.GetComponent<ColaDuelMusic>();
            if (!music) music = duel.gameObject.AddComponent<ColaDuelMusic>();
            music.match = duel;
            music.EnsureSources();
            return music;
        }

        private AudioSource Source(string name, string resource)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>(resource);
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 0;
            source.volume = 0; source.priority = 160; source.dopplerLevel = 0;
            return source;
        }

        private void EnsureSources()
        {
            if (!Ambience) Ambience = Source("BGM / After Hours", "Audio/Pizzeria_Night_Base");
            if (!Tension) Tension = Source("BGM / Pressure Pulse", "Audio/Pizzeria_Night_Tension");
            if (!Ambience.clip || !Tension.clip)
            { Debug.LogError("COLA_COLA original BGM stems are missing.", this); return; }
            if (!Ambience.isPlaying && !Tension.isPlaying)
            {
                double start = AudioSettings.dspTime + .12;
                Ambience.PlayScheduled(start); Tension.PlayScheduled(start);
            }
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !match || !Ambience || !Tension) return;
            var interaction = match.interaction;
            float pressure = interaction && interaction.Model != null ?
                Mathf.Clamp01((float)interaction.Model.Pressure / interaction.Model.Limit) : 0;
            float danger = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .9f, pressure));
            Intensity = Mathf.MoveTowards(Intensity, danger, Time.unscaledDeltaTime * .8f);
            float targetDuck = match.isActiveAndEnabled ? 1 : 0;
            var burst = interaction ? interaction.burstPresentation : null;
            if (burst)
            {
                switch (burst.Stage)
                {
                    case ColaBurstPresentation.BurstStage.Strain: targetDuck *= .5f; break;
                    case ColaBurstPresentation.BurstStage.Rupture:
                    case ColaBurstPresentation.BurstStage.Recovery: targetDuck *= .10f; break;
                    case ColaBurstPresentation.BurstStage.Blackout: targetDuck *= .18f; break;
                    case ColaBurstPresentation.BurstStage.FadeIn:
                        targetDuck *= Mathf.Lerp(.18f, 1, 1 - burst.BlackoutOpacity); break;
                }
            }
            if (match.State != ColaDuelController.DuelState.Playing) targetDuck *= .45f;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * (targetDuck < Duck ? 20 : 2.5f));
            Duck = Mathf.Lerp(Duck, targetDuck, blend);
            Ambience.volume = musicVolume * .8f * Duck;
            Tension.volume = musicVolume * .72f * Intensity * Duck;
        }

        private void OnDisable()
        {
            if (Ambience) Ambience.Stop();
            if (Tension) Tension.Stop();
        }
        private void OnDestroy()
        {
            if (Ambience) Destroy(Ambience.gameObject);
            if (Tension) Destroy(Tension.gameObject);
        }
    }
}
