using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>Owns presentation only. Pressure rolls, damage and rewards remain in the existing rule flow.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(2000)]
    public sealed class ColaBurstPresentation : MonoBehaviour
    {
        public enum BurstStage { Idle, Strain, Rupture, Recovery }
        [Range(0, 1)] public float soundVolume = .72f;
        public BurstStage Stage { get; private set; }
        public int BurstCount { get; private set; }
        public bool LastBurstWasBoss { get; private set; }
        public bool HasFlyingCap => flyingCap && flyingCap.activeSelf;
        public int LiveParticles => (liquid ? liquid.particleCount : 0) + (foam ? foam.particleCount : 0) + (mist ? mist.particleCount : 0);
        public int EmittedParticleCount { get; private set; }
        public float LensOpacity { get; private set; }
        public float MaximumAudioSample { get; private set; }
        public ColaBurstCameraImpulse CameraImpulse { get; private set; }

        private ShakeSelectionInteraction owner;
        private GameObject runtimeRoot, flyingCap;
        private ParticleSystem liquid, foam, mist;
        private AudioSource fluidAudio, impactAudio, capAudio;
        private AudioClip shakeSound, strainSound, ruptureSound, capSound;
        private Material sprayMaterial, dropMaterial;
        private Mesh dropMesh, puddleMesh;
        private Texture2D sprayTexture, vignetteTexture;
        private readonly List<Renderer> capRenderers = new List<Renderer>();
        private readonly List<bool> capVisibility = new List<bool>();
        private readonly List<Transform> puddles = new List<Transform>();
        private readonly List<Rect> lensSpots = new List<Rect>();
        private Vector3 bottleScale, capOrigin, capVelocity;
        private Quaternion capRotation;
        private float impactAt = -100, shakeStarted, tableY, anticipation, capLandAt;
        private int generation;
        private bool shaking, bossShake, capBounced;
        private readonly System.Random random = new System.Random(93117);

        public static ColaBurstPresentation EnsureFor(ShakeSelectionInteraction interaction)
        {
            var presentation = interaction.GetComponent<ColaBurstPresentation>();
            if (!presentation) presentation = interaction.gameObject.AddComponent<ColaBurstPresentation>();
            presentation.owner = interaction;
            interaction.burstPresentation = presentation;
            return presentation;
        }

        public void BeginShake(bool boss)
        {
            EnsureResources();
            CancelAndRestore();
            bossShake = boss;
            shaking = true;
            shakeStarted = Time.unscaledTime;
            bottleScale = owner.bottle.localScale;
            fluidAudio.clip = shakeSound;
            fluidAudio.loop = true;
            fluidAudio.volume = 0;
            fluidAudio.Play();
        }

        public IEnumerator PlayBurst(bool boss)
        {
            EnsureResources();
            int version = generation;
            Stage = BurstStage.Strain;
            LastBurstWasBoss = boss;
            fluidAudio.Stop();
            fluidAudio.loop = false;
            fluidAudio.clip = strainSound;
            fluidAudio.volume = soundVolume * .55f;
            fluidAudio.Play();
            float started = Time.unscaledTime;
            // A very short pressure stall makes the crack feel sudden without a long result reveal.
            while (Time.unscaledTime - started < .24f)
            {
                if (version != generation) yield break;
                anticipation = (Time.unscaledTime - started) / .24f;
                yield return null;
            }
            if (version != generation) yield break;
            anticipation = 0;
            Stage = BurstStage.Rupture;
            impactAt = Time.unscaledTime;
            BurstCount++;
            impactAudio.transform.position = MouthPosition();
            impactAudio.volume = soundVolume * (boss ? .82f : 1);
            impactAudio.PlayOneShot(ruptureSound);
            if (CameraImpulse) CameraImpulse.Fire(boss);
            LaunchCap();
            EmitSpray(boss);
            CreatePuddles();
            lensSpots.Clear();
            if (!boss)
                for (int i = 0; i < 9; i++)
                {
                    // Leave the central bottle and readouts visible; most liquid lands around the edges.
                    float x = i % 2 == 0 ? Range(.02f, .23f) : Range(.72f, .94f);
                    lensSpots.Add(new Rect(x, Range(.08f, .86f), Range(.025f, .075f), Range(.045f, .12f)));
                }
            float nextSpurt = .05f;
            while (Time.unscaledTime - impactAt < 1.05f)
            {
                if (version != generation) yield break;
                float t = Time.unscaledTime - impactAt;
                if (t >= nextSpurt && t < .43f)
                {
                    nextSpurt += .05f;
                    EmitJetSpurt(boss);
                }
                Stage = t < .15f ? BurstStage.Rupture : BurstStage.Recovery;
                if (owner.shakeAnimator) owner.shakeAnimator.SetBurstRecoil((1 - Mathf.Exp(-t * 90)) * Mathf.Exp(-t * 4));
                yield return null;
            }
            if (owner.shakeAnimator) owner.shakeAnimator.SetBurstRecoil(0);
            Stage = BurstStage.Idle;
        }

        public void FinishShake()
        {
            shaking = false;
            if (fluidAudio) fluidAudio.Stop();
            anticipation = 0;
            if (owner && owner.bottle) owner.bottle.localScale = bottleScale;
            RestoreCap();
        }

        public void CancelAndRestore()
        {
            generation++;
            Stage = BurstStage.Idle;
            anticipation = 0;
            shaking = false;
            impactAt = -100;
            LensOpacity = 0;
            if (owner && owner.shakeAnimator) owner.shakeAnimator.SetBurstRecoil(0);
            if (CameraImpulse) CameraImpulse.Clear();
            if (fluidAudio) fluidAudio.Stop();
            if (impactAudio) impactAudio.Stop();
            if (capAudio) capAudio.Stop();
            if (liquid) liquid.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (foam) foam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (mist) mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flyingCap) flyingCap.SetActive(false);
            foreach (var puddle in puddles) if (puddle) Release(puddle.gameObject);
            puddles.Clear(); lensSpots.Clear();
            RestoreCap();
            if (owner && owner.bottle && bottleScale.sqrMagnitude > 0) owner.bottle.localScale = bottleScale;
        }

        private void LateUpdate()
        {
            if (!owner || !owner.bottle || !runtimeRoot) return;
            float t = Time.unscaledTime - impactAt;
            if (shaking && fluidAudio && fluidAudio.isPlaying && Stage == BurstStage.Idle)
            {
                float grip = owner.shakeAnimator && owner.shakeAnimator.BottleHeld ? 1 : .08f;
                fluidAudio.transform.position = owner.bottle.position;
                fluidAudio.volume = soundVolume * .32f * grip;
                fluidAudio.pitch = 1 + .035f * Mathf.Sin((Time.unscaledTime - shakeStarted) * 17);
            }
            if (anticipation > 0)
                owner.bottle.localScale = Vector3.Scale(bottleScale,
                    new Vector3(1 + anticipation * .035f, 1 - anticipation * .008f, 1 + anticipation * .035f));
            else if (shaking && t >= 0 && t < .75f)
                owner.bottle.localScale = Vector3.Scale(bottleScale, new Vector3(1 + .018f * Mathf.Exp(-t * 12), 1, 1 + .018f * Mathf.Exp(-t * 12)));
            if (flyingCap && flyingCap.activeSelf && t >= 0)
            {
                Vector3 p = capOrigin + capVelocity * t + Vector3.down * (4.905f * t * t);
                float spinTime = t;
                if (t >= capLandAt)
                {
                    float landed = t - capLandAt;
                    float slide = (1 - Mathf.Exp(-landed * 5)) * .12f;
                    p.x = capOrigin.x + capVelocity.x * (capLandAt + slide);
                    p.z = capOrigin.z + capVelocity.z * (capLandAt + slide);
                    p.y = tableY + .015f + Mathf.Abs(Mathf.Sin(landed * 24)) * .055f * Mathf.Exp(-landed * 6);
                    spinTime = capLandAt + (1 - Mathf.Exp(-landed * 8)) * .08f;
                    if (!capBounced)
                    {
                        capBounced = true;
                        capAudio.transform.position = p;
                        capAudio.PlayOneShot(capSound, soundVolume * .55f);
                    }
                }
                flyingCap.transform.position = p;
                flyingCap.transform.rotation = capRotation * Quaternion.Euler(spinTime * 730, spinTime * 380, spinTime * 590);
                if (t > 2.4f) flyingCap.SetActive(false);
            }
            LensOpacity = LastBurstWasBoss || t < 0 || t > 2.25f ? 0 : .32f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 2.25f, t)));
            foreach (var puddle in puddles)
                if (puddle)
                {
                    float grow = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .45f));
                    float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.1f, 2.4f, t));
                    puddle.localScale = new Vector3(grow, 1, grow) * Mathf.Max(0, fade);
                }
        }

        private Vector3 MouthPosition() => owner.cap ? owner.cap.position : owner.bottle.position + Vector3.up * .35f;

        private void LaunchCap()
        {
            if (!owner.cap) return;
            if (flyingCap) Release(flyingCap);
            flyingCap = new GameObject("Ejected original cap");
            flyingCap.transform.SetParent(runtimeRoot.transform, false);
            flyingCap.transform.SetPositionAndRotation(owner.cap.position, owner.cap.rotation);
            capOrigin = owner.cap.position;
            capRotation = owner.cap.rotation;
            capVelocity = new Vector3(LastBurstWasBoss ? -.8f : .9f, 3.6f, .8f);
            capLandAt = (capVelocity.y + Mathf.Sqrt(capVelocity.y * capVelocity.y + 19.62f * Mathf.Max(0, capOrigin.y - tableY - .015f))) / 9.81f;
            capBounced = false;
            capRenderers.Clear(); capVisibility.Clear();
            foreach (var filter in owner.cap.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.GetComponent<MeshRenderer>();
                if (!source || !filter.sharedMesh) continue;
                var part = new GameObject("Cap mesh");
                part.transform.SetParent(flyingCap.transform, true);
                part.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                part.transform.localScale = filter.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                capRenderers.Add(source); capVisibility.Add(source.enabled);
                source.enabled = false;
            }
        }

        private void RestoreCap()
        {
            for (int i = 0; i < capRenderers.Count; i++) if (capRenderers[i]) capRenderers[i].enabled = capVisibility[i];
            capRenderers.Clear(); capVisibility.Clear();
        }

        private void EmitSpray(bool boss)
        {
            Vector3 mouth = MouthPosition();
            Vector3 towardActor = boss ? Vector3.forward : Vector3.back;
            EmittedParticleCount = 0;
            liquid.Play(); foam.Play(); mist.Play();
            for (int i = 0; i < 220; i++)
            {
                float angle = Range(0, Mathf.PI * 2), spread = Range(.05f, 1);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 velocity = Vector3.up * Range(1.6f, 4.7f) + radial * (spread * Range(.8f, 2.6f)) + towardActor * Range(.2f, 1.3f);
                var emit = new ParticleSystem.EmitParams
                {
                    position = mouth + radial * .015f,
                    velocity = velocity,
                    startLifetime = Range(.28f, 1.0f),
                    startSize = Range(.012f, .043f),
                    startColor = Color.Lerp(new Color(.16f, .065f, .018f, 1), new Color(.38f, .18f, .045f, .9f), Unit())
                };
                liquid.Emit(emit, 1); EmittedParticleCount++;
            }
            for (int i = 0; i < 150; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = mouth,
                    velocity = new Vector3(Range(-1.3f, 1.3f), Range(1, 3.5f), Range(-1.2f, 1.2f)) + towardActor * .6f,
                    startLifetime = Range(.3f, 1.15f), startSize = Range(.017f, .065f),
                    startColor = Color.Lerp(new Color(.72f, .65f, .43f, .85f), new Color(.46f, .29f, .10f, .8f), Unit())
                };
                foam.Emit(emit, 1); EmittedParticleCount++;
            }
            for (int i = 0; i < 38; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = mouth,
                    velocity = new Vector3(Range(-.6f, .6f), Range(.5f, 1.7f), Range(-.6f, .6f)) + towardActor * .4f,
                    startLifetime = Range(.35f, .9f), startSize = Range(.07f, .16f),
                    startColor = new Color(.63f, .54f, .33f, .16f)
                };
                mist.Emit(emit, 1); EmittedParticleCount++;
            }
        }

        private void CreatePuddles()
        {
            foreach (var puddle in puddles) if (puddle) Release(puddle.gameObject);
            puddles.Clear();
            Vector3 centre = owner.bottle.position;
            // The authored tabletop is the bottle's resting contact plane, not a guessed world height.
            for (int i = 0; i < 12; i++)
            {
                var go = new GameObject("Cola tabletop splash");
                go.transform.SetParent(runtimeRoot.transform, false);
                go.transform.position = new Vector3(centre.x + Range(-.4f, .4f), tableY + .003f + i * .00005f,
                    Mathf.Clamp(centre.z + Range(-.38f, .38f), -.65f, .7f));
                go.transform.rotation = Quaternion.Euler(0, Range(0, 360), 0);
                go.AddComponent<MeshFilter>().sharedMesh = puddleMesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = dropMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                puddles.Add(go.transform);
            }
        }

        private void EmitJetSpurt(bool boss)
        {
            for (int i = 0; i < 12; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = MouthPosition(),
                    velocity = new Vector3(Range(-.4f, .4f), Range(1.4f, 3.5f), (boss ? 1 : -1) * Range(.4f, 1.4f)),
                    startLifetime = Range(.3f, .75f), startSize = Range(.018f, .045f),
                    startColor = i < 6 ? new Color(.25f, .10f, .025f, 1) : new Color(.68f, .57f, .36f, .8f)
                };
                (i < 6 ? liquid : foam).Emit(emit, 1);
                EmittedParticleCount++;
            }
        }

        private void EnsureResources()
        {
            if (runtimeRoot) return;
            if (!owner) owner = GetComponent<ShakeSelectionInteraction>();
            runtimeRoot = new GameObject("COLA burst runtime effects") { hideFlags = HideFlags.DontSave };
            runtimeRoot.transform.SetParent(transform, false);
            runtimeRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var rig = owner.transform.root.Find("07_Cameras/CM_SeatedDuel");
            if (rig)
            {
                CameraImpulse = rig.GetComponent<ColaBurstCameraImpulse>();
                if (!CameraImpulse) CameraImpulse = rig.gameObject.AddComponent<ColaBurstCameraImpulse>();
            }
            var shader = Resources.Load<Shader>("COLA_COLA_Spray");
            if (!shader) throw new InvalidOperationException("COLA_COLA_Spray shader is missing.");
            sprayTexture = RadialTexture(false);
            vignetteTexture = RadialTexture(true);
            sprayMaterial = new Material(shader) { name = "Cola soft foam", hideFlags = HideFlags.DontSave };
            sprayMaterial.SetTexture("_BaseMap", sprayTexture);
            dropMaterial = new Material(shader) { name = "Cola wet facets", hideFlags = HideFlags.DontSave };
            dropMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            dropMesh = Octahedron(); puddleMesh = Puddle();
            liquid = ParticleLayer("Brown ballistic droplets", .8f, true);
            foam = ParticleLayer("Cream foam beads", .55f, false);
            mist = ParticleLayer("Fine spray cloud", .05f, false);
            fluidAudio = Source("Fluid and PET strain", .7f);
            impactAudio = Source("Rupture and pressure tail", .35f);
            capAudio = Source("Cap table impact", 1);
            shakeSound = ColaBurstAudio.Build(ColaBurstAudio.Sound.Shake);
            strainSound = ColaBurstAudio.Build(ColaBurstAudio.Sound.Strain);
            ruptureSound = ColaBurstAudio.Build(ColaBurstAudio.Sound.Rupture);
            capSound = ColaBurstAudio.Build(ColaBurstAudio.Sound.Cap);
            var samples = new float[ruptureSound.samples]; ruptureSound.GetData(samples, 0);
            MaximumAudioSample = 0;
            foreach (float value in samples) MaximumAudioSample = Mathf.Max(MaximumAudioSample, Mathf.Abs(value));
            tableY = owner.bottle.position.y;
            foreach (var renderer in owner.bottle.GetComponentsInChildren<Renderer>())
                if (!owner.cap || !renderer.transform.IsChildOf(owner.cap)) tableY = Mathf.Min(tableY, renderer.bounds.min.y);
            var plane = new GameObject("Tabletop liquid collision plane");
            plane.transform.SetParent(runtimeRoot.transform, false);
            plane.transform.position = Vector3.up * tableY;
            foreach (var particles in new[] { liquid, foam })
            {
                var collision = particles.collision;
                collision.enabled = true; collision.type = ParticleSystemCollisionType.Planes;
                collision.AddPlane(plane.transform);
                collision.bounce = .12f; collision.dampen = .55f; collision.lifetimeLoss = .35f;
            }
        }

        private AudioSource Source(string name, float spatial)
        {
            var go = new GameObject(name); go.transform.SetParent(runtimeRoot.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = spatial;
            source.rolloffMode = AudioRolloffMode.Logarithmic; source.minDistance = 1.5f; source.maxDistance = 9;
            source.dopplerLevel = 0; source.priority = name.StartsWith("Rupture") ? 20 : 80;
            return source;
        }

        private ParticleSystem ParticleLayer(string name, float gravity, bool mesh)
        {
            var go = new GameObject(name); go.transform.SetParent(runtimeRoot.transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = false; main.duration = 2.5f; main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = true;
            main.gravityModifier = gravity; main.startSpeed = 0;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var colour = particles.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .55f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .75f), new Keyframe(.25f, 1), new Keyframe(1, .25f)));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mesh ? dropMaterial : sprayMaterial;
            renderer.renderMode = mesh ? ParticleSystemRenderMode.Mesh : ParticleSystemRenderMode.Billboard;
            if (mesh) renderer.mesh = dropMesh;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static Mesh Octahedron()
        {
            var mesh = new Mesh { name = "Low poly liquid bead", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { Vector3.up * .75f, Vector3.down * .75f, Vector3.left * .45f, Vector3.right * .45f, Vector3.forward * .45f, Vector3.back * .45f };
            mesh.triangles = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private Mesh Puddle()
        {
            const int sides = 12;
            var vertices = new Vector3[sides + 1]; var colours = new Color[sides + 1]; var triangles = new int[sides * 3];
            colours[0] = new Color(.09f, .035f, .008f, .7f);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, r = Range(.025f, .09f);
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r * .7f);
                colours[i + 1] = new Color(.18f, .07f, .015f, .05f);
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % sides + 1;
            }
            var mesh = new Mesh { name = "Faceted cola splash", hideFlags = HideFlags.DontSave, vertices = vertices, colors = colours, triangles = triangles };
            mesh.RecalculateBounds(); return mesh;
        }

        private static Texture2D RadialTexture(bool vignette)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = vignette ? "Impact edge mask" : "Soft spray mask", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = vignette ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, 1.15f, r)) : Mathf.Pow(Mathf.Max(0, 1 - r * r), 2);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }

        private void OnGUI()
        {
            float t = Time.unscaledTime - impactAt;
            if (!runtimeRoot || t < 0 || t > 2.25f || Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color; int depth = GUI.depth; GUI.depth = -2;
            GUI.color = new Color(.05f, .025f, .012f, (LastBurstWasBoss ? .1f : .42f) * Mathf.Exp(-t * 2.8f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteTexture);
            GUI.color = new Color(.24f, .10f, .025f, LensOpacity);
            foreach (var spot in lensSpots)
                GUI.DrawTexture(new Rect(spot.x * Screen.width, (spot.y + t * .015f) * Screen.height, spot.width * Screen.width, spot.height * Screen.height), sprayTexture);
            GUI.color = previous; GUI.depth = depth;
        }

        private float Unit() => (float)random.NextDouble();
        private float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, Unit());

        private void OnDisable() => CancelAndRestore();
        private static void Release(UnityEngine.Object resource)
        {
            if (!resource) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
        private void OnDestroy()
        {
            CancelAndRestore();
            foreach (var resource in new UnityEngine.Object[] { runtimeRoot, sprayMaterial, dropMaterial, sprayTexture, vignetteTexture,
                dropMesh, puddleMesh, shakeSound, strainSound, ruptureSound, capSound }) Release(resource);
        }
    }
}
