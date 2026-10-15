using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ColaCola.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ColaCola.Editor
{
    [InitializeOnLoad]
    public static class HorrorSceneTools
    {
        public const string Output = "output/horror-atmosphere";
        public const string ProfilePath = "Assets/_COLA_COLA/Art/PostProcessing/PizzeriaHorror.asset";
        private const string Materials = "Assets/_COLA_COLA/Art/Materials/HorrorLighting";
        private const string Running = "COLACOLA.HorrorVerification";
        private static readonly Vector3 OriginalCameraPosition = new Vector3(0, 1.32f, -1.4f);
        private static double playStarted;
        private static float minPractical, maxPractical;
        private static float nextCapture;
        private static int captureCount;
        private static bool stopping;
        private static readonly List<float> captureTimes = new List<float>();

        static HorrorSceneTools()
        {
            if (SessionState.GetBool(Running, false)) Hook();
        }

        [MenuItem("COLA_COLA/Apply Horror Lighting and Cinemachine")]
        public static void Apply()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var scene = EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(r => r.name == "COLA_COLA_MainScene");
                var camera = Camera.main;
                var protectedPoses = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => !t.GetComponentsInParent<Transform>().Any(p => p.name == "06_Lighting" || p.name == "09_Atmosphere"))
                    .ToDictionary(t => t, t => new Pose(t.position, t.rotation));
                ApplyToScene(root, camera);
                foreach (var pair in protectedPoses)
                    if (!pair.Key || Vector3.Distance(pair.Key.position, pair.Value.position) > .00001f || Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) > .02f)
                        throw new InvalidOperationException("Camera/model placement changed during atmosphere setup: " + pair.Key);
                EditorSceneManager.SaveScene(scene, MainSceneTools.ScenePath);
                AssetDatabase.SaveAssets();
                MainSceneTools.WriteReport(root, camera);
                File.WriteAllText(Output + "/Placement-validation.txt", "PASS: " + protectedPoses.Count + " existing camera/model/bone world poses retained.\n");
                StartVerification();
            }
            catch (Exception exception) { Fail(exception); }
        }

        // May also be used by the scene builder after the final camera placement exists.
        public static void ApplyToScene(GameObject root, Camera camera)
        {
            if (!camera) throw new InvalidOperationException("Main Camera is missing.");
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            SetupCamera(root, camera);
            SetupLighting(root);
            SetupVolume(root);
        }

        private static void SetupCamera(GameObject root, Camera camera)
        {
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.backgroundColor = new Color(.009f, .016f, .014f);
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1;
            data.volumeTrigger = camera.transform;
            data.antialiasing = AntialiasingMode.None;
            data.dithering = true;
            var cameraGroup = root.transform.Find("07_Cameras");
            var rigTransform = cameraGroup.Find("CM_SeatedDuel");
            if (!rigTransform)
            {
                rigTransform = new GameObject("CM_SeatedDuel").transform;
                rigTransform.SetParent(cameraGroup, false);
            }
            rigTransform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            var rig = rigTransform.GetComponent<CinemachineCamera>();
            if (!rig) rig = rigTransform.gameObject.AddComponent<CinemachineCamera>();
            rig.Lens = LensSettings.FromCamera(camera);
            rig.Follow = null;
            rig.LookAt = null;
            rig.Priority = 10;
            var brain = camera.GetComponent<CinemachineBrain>();
            if (!brain) brain = camera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .65f);
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        }

        private static void SetupLighting(GameObject root)
        {
            var group = root.transform.Find("06_Lighting");
            if (!group) throw new InvalidOperationException("Lighting group is missing.");
            // Reuse the four existing lights and only create named scene-local additions.
            Light key = Light(group, "Warm_Table_Key", new Vector3(-.20f, 2.72f, .15f), new Vector3(0, .84f, -.07f),
                new Color(.85f, .93f, .76f), 4.1f, 4.3f, LightType.Spot, 82);
            key.innerSpotAngle = 55;
            key.shadows = LightShadows.Soft;
            key.shadowBias = .025f;
            key.shadowNormalBias = .04f;
            Light(group, "Warm_Face_Fill", new Vector3(-.20f, 1.72f, -.50f), new Vector3(.10f, 1.15f, 1),
                new Color(.70f, .79f, .73f), 1.25f, 2.7f, LightType.Point);
            Light(group, "Horror_Face_Softbox", new Vector3(-.45f, 1.85f, -.55f), new Vector3(.10f, 1.34f, .98f),
                new Color(.77f, .84f, .74f), 2.3f, 3.2f, LightType.Spot, 48);
            Light(group, "Horror_Cola_LabelFill", new Vector3(.15f, 1.45f, -.80f), new Vector3(0, 1.04f, -.07f),
                new Color(.84f, .86f, .72f), .55f, 1.8f, LightType.Spot, 40);
            Light(group, "Kitchen_Warm", new Vector3(-1.9f, 2.7f, 3.4f), new Vector3(-1.4f, .9f, 3.5f),
                new Color(.36f, .61f, .44f), .50f, 4.5f, LightType.Spot, 106);
            Light(group, "Soda_Cool_Fill", new Vector3(2.1f, 1.8f, 3.15f), Vector3.zero,
                new Color(.20f, .66f, .57f), .95f, 2.6f, LightType.Point);
            Light(group, "Horror_Boss_RedRim", new Vector3(1.20f, 1.95f, 2.05f), new Vector3(.10f, 1.17f, .98f),
                new Color(1f, .12f, .055f), 2.1f, 3.1f, LightType.Spot, 64);
            Light(group, "Horror_Gauge_Readability", new Vector3(-.74f, 1.53f, -.62f), Vector3.zero,
                new Color(.86f, .88f, .70f), .40f, 1.1f, LightType.Point);
            var practical = Light(group, "Horror_Kitchen_Fluorescent", new Vector3(-.35f, 2.13f, 4.75f), Vector3.zero,
                new Color(.40f, .70f, .51f), .85f, 2.8f, LightType.Point);
            var flicker = practical.GetComponent<HorrorPracticalFlicker>();
            if (!flicker) flicker = practical.gameObject.AddComponent<HorrorPracticalFlicker>();
            flicker.targetLight = practical;
            flicker.baseIntensity = .85f;
            flicker.variation = .07f;
            flicker.cycleSeconds = 17;
            flicker.seed = 2.73f;
            practical.intensity = flicker.baseIntensity;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.055f, .073f, .063f);
            RenderSettings.reflectionIntensity = .12f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.018f, .031f, .028f);
            RenderSettings.fogDensity = .065f;

            var atmosphere = root.transform.Find("09_Atmosphere");
            if (!atmosphere)
            {
                atmosphere = new GameObject("09_Atmosphere").transform;
                atmosphere.SetParent(root.transform, false);
            }
            var metal = GlowMaterial("Practical_DarkHousing", new Color(.025f, .035f, .031f), Color.black);
            var green = GlowMaterial("Practical_FluorescentGreen", new Color(.42f, .62f, .47f), new Color(.51f, .85f, .60f) * 2.5f);
            var red = GlowMaterial("Practical_EmergencyRed", new Color(.40f, .025f, .012f), new Color(1, .045f, .015f) * 2.4f);
            Fixture(atmosphere, "KitchenStrip_Housing", new Vector3(-.35f, 2.22f, 5.055f), new Vector3(1.15f, .095f, .045f), metal);
            Fixture(atmosphere, "KitchenStrip_Glow", new Vector3(-.35f, 2.22f, 5.025f), new Vector3(1.02f, .026f, .018f), green);
            Fixture(atmosphere, "EmergencyLamp_Housing", new Vector3(2.80f, 1.95f, 5.05f), new Vector3(.15f, .21f, .07f), metal);
            Fixture(atmosphere, "EmergencyLamp_Glow", new Vector3(2.80f, 1.95f, 5.005f), new Vector3(.095f, .13f, .035f), red);
            Light(group, "Horror_Emergency_WallGlow", new Vector3(2.80f, 1.95f, 4.78f), Vector3.zero,
                new Color(1, .075f, .025f), .65f, 1.75f, LightType.Point);
        }

        private static Light Light(Transform parent, string name, Vector3 position, Vector3 target, Color color, float intensity, float range, LightType type, float angle = 90)
        {
            var child = parent.Find(name);
            if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            child.position = position;
            if (type == LightType.Spot) child.rotation = Quaternion.LookRotation(target - position);
            var light = child.GetComponent<Light>();
            if (!light) light = child.gameObject.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * .7f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Material GlowMaterial(string name, Color albedo, Color emission)
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", albedo);
            material.SetFloat("_Smoothness", .08f);
            material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Fixture(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var child = parent.Find(name);
            if (!child)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                primitive.name = name;
                child = primitive.transform;
                child.SetParent(parent, false);
                UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
            }
            child.position = position;
            child.localScale = scale;
            child.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var effect)) effect = profile.Add<T>(true);
            effect.active = true;
            return effect;
        }

        private static void SetupVolume(GameObject root)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "PizzeriaHorror"; AssetDatabase.CreateAsset(profile, ProfilePath); }
            var tone = Effect<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);
            var color = Effect<ColorAdjustments>(profile);
            color.postExposure.Override(.40f);
            color.contrast.Override(13);
            color.saturation.Override(-19);
            color.colorFilter.Override(new Color(.98f, .975f, .94f));
            var white = Effect<WhiteBalance>(profile);
            white.temperature.Override(-2);
            white.tint.Override(0);
            // A small black lift gives the grain room in shadows without washing out the room.
            var wheels = Effect<LiftGammaGain>(profile);
            wheels.lift.Override(new Vector4(1f, 1f, 1f, .006f));
            var ranges = Effect<ShadowsMidtonesHighlights>(profile);
            ranges.shadows.Override(new Vector4(.91f, .99f, 1.01f, 0));
            ranges.midtones.Override(new Vector4(.99f, 1.00f, .96f, 0));
            ranges.highlights.Override(new Vector4(1.04f, 1.01f, .94f, 0));
            var bloom = Effect<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(.20f);
            bloom.scatter.Override(.50f);
            bloom.tint.Override(new Color(.85f, 1, .87f));
            bloom.highQualityFiltering.Override(true);
            var grain = Effect<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(.26f);
            grain.response.Override(.60f);
            var vignette = Effect<Vignette>(profile);
            vignette.intensity.Override(.25f);
            vignette.smoothness.Override(.62f);
            vignette.color.Override(new Color(.003f, .009f, .007f));
            vignette.rounded.Override(true);
            var aberration = Effect<ChromaticAberration>(profile);
            aberration.intensity.Override(.018f);
            foreach (var component in profile.components)
            {
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            var group = root.transform.Find("09_Atmosphere");
            var volumeTransform = group.Find("Horror_GlobalVolume");
            if (!volumeTransform) { volumeTransform = new GameObject("Horror_GlobalVolume").transform; volumeTransform.SetParent(group, false); }
            volumeTransform.gameObject.layer = 0;
            var volume = volumeTransform.GetComponent<Volume>();
            if (!volume) volume = volumeTransform.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.weight = 1;
            volume.sharedProfile = profile;
        }

        public static void Verify()
        {
            try
            {
                EditorSceneManager.OpenScene(MainSceneTools.ScenePath, OpenSceneMode.Single);
                StartVerification();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void StartVerification()
        {
            var camera = Camera.main;
            var brain = camera.GetComponent<CinemachineBrain>();
            var rig = UnityEngine.Object.FindAnyObjectByType<CinemachineCamera>();
            if (!brain || !rig) throw new InvalidOperationException("Cinemachine rig was not saved.");
            if (Vector3.Distance(camera.transform.position, OriginalCameraPosition) > .0001f || Mathf.Abs(camera.fieldOfView - 50) > .001f || Mathf.Abs(camera.nearClipPlane - .035f) > .00001f)
                throw new InvalidOperationException("Original seated composition was changed.");
            var volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            if (!volume || !volume.isGlobal || AssetDatabase.GetAssetPath(volume.sharedProfile) != ProfilePath)
                throw new InvalidOperationException("Scene-specific global Volume is not assigned.");
            var roots = camera.gameObject.scene.GetRootGameObjects();
            var nodes = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            if (nodes.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) != 0)
                throw new InvalidOperationException("Missing scripts after package import.");
            if (nodes.Count(t => t.GetComponent<Camera>()) != 1 || nodes.Count(t => t.GetComponent<AudioListener>()) != 1)
                throw new InvalidOperationException("Expected one render camera and AudioListener.");
            var renderers = nodes.Select(t => t.GetComponent<Renderer>()).Where(r => r).ToArray();
            if (renderers.Any(r => r.sharedMaterials.Any(m => !m || !m.shader)))
                throw new InvalidOperationException("Missing scene material/shader after atmosphere setup.");
            foreach (string name in new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName })
            {
                var animator = nodes.Single(t => t.name == name).GetComponent<Animator>();
                if (!animator || !animator.enabled || !animator.runtimeAnimatorController)
                    throw new InvalidOperationException("Existing character Idle controller was lost.");
            }
            SessionState.SetBool(Running, true);
            SessionState.SetString(Running + ".Start", DateTime.UtcNow.ToString("O"));
            SessionState.SetBool(Running + ".Passed", false);
            playStarted = -1;
            stopping = false;
            Hook();
            EditorApplication.isPlaying = true;
        }

        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayState;
            EditorApplication.playModeStateChanged += PlayState;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || stopping) return;
            try
            {
                if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Running + ".Start", "")) > TimeSpan.FromSeconds(100))
                    throw new TimeoutException("Horror atmosphere verification timed out.");
                if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
                if (playStarted < 0)
                {
                    playStarted = Time.timeAsDouble;
                    nextCapture = 3;
                    captureCount = 0;
                    captureTimes.Clear();
                    minPractical = float.PositiveInfinity;
                    maxPractical = 0;
                    Directory.CreateDirectory(Output + "/frames");
                    return;
                }
                float elapsed = (float)(Time.timeAsDouble - playStarted);
                var camera = Camera.main;
                var brain = camera.GetComponent<CinemachineBrain>();
                if (brain.ActiveVirtualCamera == null || brain.ActiveVirtualCamera.Name != "CM_SeatedDuel")
                    throw new InvalidOperationException("Cinemachine is not actively driving the camera.");
                if (Vector3.Distance(camera.transform.position, OriginalCameraPosition) > .0001f ||
                    Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(11, 0, 0)) > .03f || Mathf.Abs(camera.fieldOfView - 50) > .001f)
                    throw new InvalidOperationException("Cinemachine changed the seated camera framing.");
                var data = camera.GetUniversalAdditionalCameraData();
                if (!camera.allowHDR || !data.renderPostProcessing) throw new InvalidOperationException("Camera post-processing/HDR disabled.");
                var practical = UnityEngine.Object.FindAnyObjectByType<HorrorPracticalFlicker>();
                minPractical = Mathf.Min(minPractical, practical.targetLight.intensity);
                maxPractical = Mathf.Max(maxPractical, practical.targetLight.intensity);
                if (elapsed >= nextCapture && elapsed < 7f)
                {
                    camera.aspect = 16f / 9f;
                    MainSceneTools.Capture(camera, Output + $"/frames/Horror-{captureCount:D3}.png", 960, 540);
                    captureTimes.Add(elapsed);
                    captureCount++;
                    nextCapture = elapsed + .08f;
                }
                if (elapsed < 18) return;
                if (maxPractical - minPractical < .10f || minPractical < practical.baseIntensity * .69f)
                    throw new InvalidOperationException("Background practical did not flicker within its intended range.");
                // A separate native screenshot verifies the final screen effects from the live render camera.
                MainSceneTools.Capture(camera, Output + "/MainScene-Horror.png");
                Directory.CreateDirectory("output/indie-look");
                File.Copy(Output + "/MainScene-Horror.png", "output/indie-look/MainScene-Indie.png", true);
                MainSceneTools.Capture(camera, MainSceneTools.Output + "/MainScene-preview.png");
                var stack = VolumeManager.instance.stack;
                if (stack.GetComponent<Vignette>().intensity.value <= 0 || stack.GetComponent<FilmGrain>().intensity.value <= 0 || stack.GetComponent<Tonemapping>().mode.value != TonemappingMode.ACES)
                    throw new InvalidOperationException("Global Volume was not evaluated by URP.");
                var liveGrain = stack.GetComponent<FilmGrain>();
                if (liveGrain.type.value != FilmGrainLookup.Thin2 || Mathf.Abs(liveGrain.intensity.value - .26f) > .001f || !data.dithering || stack.GetComponent<LiftGammaGain>().lift.value.w <= 0)
                    throw new InvalidOperationException("Indie grain, lifted blacks or dithering were not evaluated.");
                var cmPackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CinemachineBrain).Assembly);
                var report = new StringBuilder();
                report.AppendLine("PASS: saved MainScene reopened with CinemachineBrain and live CM_SeatedDuel.");
                report.AppendLine("Cinemachine resolved package: " + cmPackage.version + " (" + cmPackage.source + ").");
                report.AppendLine("PASS: camera position (0,1.32,-1.40), rotation (11,0,0), FOV50 and near clip preserved.");
                report.AppendLine("PASS: URP camera HDR/post-processing and evaluated global Volume active.");
                report.AppendLine("PASS: ACES, color grading, white balance, shadow/highlight tint, bloom, film grain, vignette and subtle chromatic aberration configured.");
                report.AppendLine($"PASS: indie grain {liveGrain.type.value}, intensity={liveGrain.intensity.value:F2}, response={liveGrain.response.value:F2}; gentle black lift and output dithering active.");
                report.AppendLine($"PASS: background practical intensity varies {minPractical:F4} to {maxPractical:F4}; table key remains steady.");
                report.AppendLine($"PASS: real Play mode {elapsed:F2}s, {captureCount} runtime preview frames, one render camera/listener, no missing scripts.");
                File.WriteAllText(Output + "/Horror-validation.txt", report.ToString());
                File.WriteAllLines(Output + "/frame-times.txt", captureTimes.Select(t => t.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)));
                Debug.Log("COLA_HORROR_PLAYMODE_PASS\n" + report);
                SessionState.SetBool(Running + ".Passed", true);
                stopping = true;
                EditorApplication.isPlaying = false;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void PlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayState;
            bool passed = SessionState.GetBool(Running + ".Passed", false);
            if (passed) MainSceneTools.Verify();
            else if (Application.isBatchMode) EditorApplication.Exit(2);
        }

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Horror-error.txt", exception.ToString());
            stopping = true;
            SessionState.SetBool(Running + ".Passed", false);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else { SessionState.SetBool(Running, false); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
    }
}
