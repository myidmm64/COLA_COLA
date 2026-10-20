using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>One Bleak Terminal loop, ducked during result and explosion presentations.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaDuelMusic : MonoBehaviour
    {
        public const string MusicResource = "Audio/BleakTerminal";
        [Range(0, 1)] public float musicVolume = .50f;
        public AudioSource Ambience { get; private set; }
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
            // Retire the old pressure layer after an editor script reload as well as on a fresh run.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == "BGM / Last Order - Pressure")
                {
                    var oldSource = child.GetComponent<AudioSource>();
                    if (oldSource) oldSource.Stop();
                    Destroy(child.gameObject);
                }
                else if (!Ambience && (child.name == "BGM / Bleak Terminal" || child.name == "BGM / Last Order - Drive"))
                    Ambience = child.GetComponent<AudioSource>();
            }
            if (!Ambience) Ambience = Source("BGM / Bleak Terminal", MusicResource);
            var clip = Resources.Load<AudioClip>(MusicResource);
            if (Ambience.clip != clip)
            {
                Ambience.Stop();
                Ambience.clip = clip;
            }
            Ambience.name = "BGM / Bleak Terminal";
            if (!Ambience.clip)
            { Debug.LogError("COLA_COLA Bleak Terminal BGM is missing.", this); return; }
            if (!Ambience.isPlaying)
            {
                Ambience.Play();
            }
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !match || !Ambience) return;
            var interaction = match.interaction;
            float pressure = interaction && interaction.Model != null ?
                Mathf.Clamp01((float)interaction.Model.Pressure / interaction.Model.Limit) : 0;
            float danger = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .9f, pressure));
            Intensity = Mathf.MoveTowards(Intensity, danger, Time.unscaledDeltaTime * .8f);
            float targetDuck = match.isActiveAndEnabled ? 1 : 0;
            if (interaction && interaction.AwaitingShakeResult) targetDuck *= .58f;
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
            Ambience.volume = musicVolume * .84f * Duck;
        }

        private void OnEnable()
        {
            if (Application.IsPlaying(gameObject) && match) EnsureSources();
        }

        private void OnDisable()
        {
            if (Ambience) Ambience.Stop();
        }
        private void OnDestroy()
        {
            if (Ambience) Destroy(Ambience.gameObject);
        }
    }
}
