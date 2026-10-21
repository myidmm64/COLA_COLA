using UnityEngine;
using UnityEngine.Rendering;
using Unity.Cinemachine;
using System.Collections.Generic;

namespace ColaCola.Presentation
{
    /// <summary>Menu-only atmosphere with its own real-time particles; never advances the duel clock.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(20000)]
    public sealed class ColaMenuBackdrop : MonoBehaviour
    {
        private GameObject root;
        private ParticleSystem motes;
        private Material material;
        private bool visible;
        private Camera view;
        private CinemachineBrain brain;
        private bool brainWasEnabled;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedFov;
        private Vector3 menuPosition, menuTarget;
        private readonly Dictionary<Renderer, bool> hiddenArms = new();
        private GameObject lighting;
        private ColaBossIdleVariation bossIdle;

        public void SetVisible(bool value, Camera camera)
        {
            if (visible == value) return;
            if (value && camera)
            {
                view = camera; savedPosition = view.transform.position; savedRotation = view.transform.rotation; savedFov = view.fieldOfView;
                brain = view.GetComponent<CinemachineBrain>(); brainWasEnabled = brain && brain.enabled;
                if (brain) brain.enabled = false;
                var match = GetComponent<ColaDuelController>();
                var interaction = match ? match.interaction : GetComponent<ShakeSelectionInteraction>();
                if (interaction)
                {
                    bossIdle = interaction.GetComponent<ColaBossIdleVariation>();
                    if (!bossIdle) { ColaBossIdleVariation.EnsureFor(interaction); bossIdle = interaction.GetComponent<ColaBossIdleVariation>(); }
                    if (bossIdle) bossIdle.SetMenuIdle(true);
                }
                Vector3 bottle = interaction && interaction.bottle ? interaction.bottle.position : transform.position + new Vector3(0, .9f, 0);
                menuPosition = bottle + new Vector3(-.62f, .52f, -1.18f);
                menuTarget = bottle + new Vector3(-.42f, .20f, .16f);
                view.fieldOfView = 49;
                view.transform.SetPositionAndRotation(menuPosition, Quaternion.LookRotation(menuTarget - menuPosition));
                var actor = interaction && interaction.shakeAnimator ? interaction.shakeAnimator.playerAnimator : null;
                if (actor) foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
                { hiddenArms[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
                if (interaction && interaction.gauge) HideForMenu(interaction.gauge.transform.parent);
                if (match && match.terminal) HideForMenu(match.terminal.transform);
                var inventories = GetComponent<ColaItemPresentation>();
                if (inventories) { HideForMenu(inventories.StorageRoot()); HideForMenu(inventories.StorageRoot(true)); }
                if (!lighting) CreateLighting(bottle);
                lighting.SetActive(true);
            }
            if (value && !motes && camera) Create(camera);
            visible = value;
            if (motes)
            {
                root.SetActive(value);
                if (value) motes.Play();
                else motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (!value) RestoreView();
        }

        private void HideForMenu(Transform item)
        {
            if (!item) return;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                if (!hiddenArms.ContainsKey(renderer)) { hiddenArms[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
        }

        private void LateUpdate()
        {
            if (!visible || !view) return;
            float time = Time.unscaledTime;
            Vector3 offset = new Vector3(Mathf.Sin(time * .11f) * .018f, Mathf.Sin(time * .15f) * .006f, Mathf.Cos(time * .11f) * .009f);
            view.transform.SetPositionAndRotation(menuPosition + offset, Quaternion.LookRotation(menuTarget - menuPosition - offset));
        }

        private void CreateLighting(Vector3 bottle)
        {
            lighting = new GameObject("Menu / Table Practical") { hideFlags = HideFlags.DontSave };
            lighting.transform.SetParent(transform, false);
            var key = new GameObject("Soft Amber Table Light").AddComponent<Light>();
            key.transform.SetParent(lighting.transform, false);
            key.transform.position = bottle + new Vector3(1.0f, 1.3f, -.8f);
            key.transform.rotation = Quaternion.LookRotation(bottle + Vector3.up * .15f - key.transform.position);
            key.type = LightType.Spot; key.color = new Color(.88f, .77f, .58f); key.intensity = 1.8f;
            key.range = 4; key.spotAngle = 55; key.innerSpotAngle = 32; key.shadows = LightShadows.Soft;
        }

        private void RestoreView()
        {
            if (bossIdle) bossIdle.SetMenuIdle(false);
            bossIdle = null;
            if (view) { view.transform.SetPositionAndRotation(savedPosition, savedRotation); view.fieldOfView = savedFov; }
            if (brain) brain.enabled = brainWasEnabled;
            foreach (var pair in hiddenArms) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
            hiddenArms.Clear();
            if (lighting) lighting.SetActive(false);
            view = null;
        }

        private void Create(Camera camera)
        {
            var shader = Resources.Load<Shader>("COLA_COLA_MenuDust");
            if (!shader) return;
            root = new GameObject("Menu / Quiet Dust") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(camera.transform.TransformPoint(new Vector3(.25f, .12f, 1.9f)), camera.transform.rotation);
            motes = root.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.loop = true; main.playOnAwake = false; main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(11, 18);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.006f, .018f);
            main.startSize = new ParticleSystem.MinMaxCurve(.004f, .010f);
            main.startColor = new Color(.73f, .75f, .66f, .24f);
            main.maxParticles = 28;
            var emission = motes.emission; emission.rateOverTime = 1.7f;
            var shape = motes.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(2.2f, 1.1f, 1.0f);
            var velocity = motes.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.009f, .009f);
            velocity.y = new ParticleSystem.MinMaxCurve(.003f, .011f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.005f, .005f);
            var colors = motes.colorOverLifetime; colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.7f, .2f), new GradientAlphaKey(.7f, .75f), new GradientAlphaKey(0, 1) });
            colors.color = gradient;
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            material = new Material(shader) { name = "MenuDust / runtime", hideFlags = HideFlags.DontSave };
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            motes.randomSeed = 2101026;
            motes.Simulate(8, true, true); // Populate the still-open restaurant immediately on menu entry.
            root.SetActive(false);
        }

        private void OnDisable()
        { if (motes) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); RestoreView(); visible = false; }

        private void OnDestroy()
        { RestoreView(); if (root) Destroy(root); if (lighting) Destroy(lighting); if (material) Destroy(material); }
    }
}
