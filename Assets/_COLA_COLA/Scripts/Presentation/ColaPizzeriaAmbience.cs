using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Quiet room machinery and sparse incidental sounds; independent of all game RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaPizzeriaAmbience : MonoBehaviour
    {
        [Range(0, 1)] public float ambienceVolume = .28f;
        private ColaDuelController match;
        private ColaDuelMusic music;
        private AudioSource fridge, vent, incidental;
        private AudioClip metal, drip;
        private Vector3 fridgePosition, ventPosition;
        private readonly System.Random timing = new System.Random(416031);
        private float nextSound;

        public static ColaPizzeriaAmbience EnsureFor(ColaDuelController duel)
        {
            var room = duel.GetComponent<ColaPizzeriaAmbience>();
            if (!room) room = duel.gameObject.AddComponent<ColaPizzeriaAmbience>();
            room.match = duel;
            room.music = duel.GetComponent<ColaDuelMusic>();
            room.EnsureSources();
            return room;
        }

        private Vector3 RoomPosition(string objectName, Vector3 fallback, Vector3 offset)
        {
            foreach (var candidate in transform.root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate.position + offset;
            return fallback;
        }

        private AudioSource Source(string name, Vector3 position, string resource, bool loop, float spatial)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(transform, false);
            child.transform.position = position;
            var source = child.AddComponent<AudioSource>();
            source.clip = string.IsNullOrEmpty(resource) ? null : Resources.Load<AudioClip>(resource);
            source.playOnAwake = false; source.loop = loop;
            source.spatialBlend = spatial; source.dopplerLevel = 0;
            source.minDistance = 1.2f; source.maxDistance = 9;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.priority = 210; source.volume = 0;
            return source;
        }

        private void EnsureSources()
        {
            if (!fridge)
            {
                fridgePosition = RoomPosition("SodaFountain_ExistingModel", new Vector3(1.6f, 1.1f, 2.2f), Vector3.up * .65f);
                ventPosition = RoomPosition("PizzaOven_ExistingModel", new Vector3(-1.5f, 2.1f, 1.8f), Vector3.up * 1.1f);
                fridge = Source("Room / Refrigerator Motor", fridgePosition, "Audio/Pizzeria_Fridge_Room", true, .45f);
                vent = Source("Room / Ventilation Air", ventPosition, "Audio/Pizzeria_Vent_Air", true, .8f);
                incidental = Source("Room / Quiet Metal And Pipe", fridgePosition, null, false, .85f);
                metal = Resources.Load<AudioClip>("Audio/Pizzeria_Metal_Settle");
                drip = Resources.Load<AudioClip>("Audio/Pizzeria_Pipe_Drip");
                nextSound = Time.unscaledTime + 6 + (float)timing.NextDouble() * 4;
            }
            if (fridge.clip && !fridge.isPlaying) fridge.Play();
            if (vent.clip && !vent.isPlaying) vent.Play();
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject) || !match || !fridge || !vent || !incidental) return;
            // The music's duck follows the actual explosion/blackout, keeping critical effects audible.
            float duck = music ? Mathf.Lerp(.16f, 1, music.Duck) : 1;
            float level = match.isActiveAndEnabled ? ambienceVolume * duck : 0;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 7);
            fridge.volume = Mathf.Lerp(fridge.volume, level * .55f, blend);
            vent.volume = Mathf.Lerp(vent.volume, level * .40f, blend);
            incidental.volume = level * .65f;
            if (Time.unscaledTime < nextSound) return;
            nextSound = Time.unscaledTime + 12 + (float)timing.NextDouble() * 11;
            if (!match.isActiveAndEnabled || match.State != ColaDuelController.DuelState.Playing ||
                (music && music.Duck < .5f)) return;
            bool isMetal = timing.Next(3) == 0;
            var clip = isMetal ? metal : drip;
            if (!clip) return;
            incidental.transform.position = isMetal ? ventPosition : fridgePosition;
            incidental.PlayOneShot(clip, isMetal ? .65f : .5f);
        }

        private void OnEnable()
        {
            if (Application.IsPlaying(gameObject) && match) EnsureSources();
        }

        private void OnDisable()
        {
            if (fridge) fridge.Stop();
            if (vent) vent.Stop();
            if (incidental) incidental.Stop();
        }

        private void OnDestroy()
        {
            if (fridge) Destroy(fridge.gameObject);
            if (vent) Destroy(vent.gameObject);
            if (incidental) Destroy(incidental.gameObject);
        }
    }
}
