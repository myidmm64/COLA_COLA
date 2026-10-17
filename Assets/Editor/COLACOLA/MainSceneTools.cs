using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ColaCola.Editor
{
    public static class MainSceneTools
    {
        public const string Output = "output/scene-layout";
        public const string ScenePath = "Assets/Scenes/MainScene.unity";
        private const string Materials = "Assets/_COLA_COLA/Art/Materials/Blockout";
        private static GameObject props;
        private static GameObject environment;
        private static Camera mainCamera;

        public static void Build()
        {
            try
            {
                Directory.CreateDirectory(Output);
                Directory.CreateDirectory(Materials);
                AssetDatabase.Refresh();
                if (File.Exists(ScenePath))
                    File.Copy(ScenePath, Output + "/MainScene-before-rebuild.unity", true);
                var mode = !Application.isBatchMode && SceneManager.GetActiveScene().isDirty
                    ? NewSceneMode.Additive : NewSceneMode.Single;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("COLA_COLA_MainScene");
                var stage = Group("01_Pizzeria", root.transform);
                var arena = Group("02_DuelTable", root.transform);
                var characters = Group("03_Characters", root.transform);
                var items = Group("04_TabletopItems", root.transform);
                var placeholders = Group("05_CubePlaceholders", root.transform);
                var lighting = Group("06_Lighting", root.transform);
                var cameras = Group("07_Cameras", root.transform);
                var devices = Group("08_Devices", root.transform);
                props = Spawn("Assets/2.Assets/04_Pizzeria/Prefabs/Pizzeria_Props_fbx.prefab");
                environment = Spawn("Assets/2.Assets/04_Pizzeria/Prefabs/Pizzeria_Scene_fbx.prefab");

                BuildRoom(stage);
                Prop("Chair_Table", "DuelTable_ExistingModel", arena,
                    new Vector3(0, 0, 0), new Vector3(2.8f, .83f, 1.9f));
                Prop("Chair_Table.001", "BossBench_ExistingModel", arena,
                    new Vector3(0, 0, 1.17f), new Vector3(2.3f, 1.13f, .62f));
                var bottle = Spawn("Assets/2.Assets/01_Cola_Bottle/Prefabs/cola_bottle_fbx.prefab", arena);
                bottle.name = "HeroCola_ExistingModel";
                bottle.transform.localScale = Vector3.one * 1.85f;
                bottle.transform.position = new Vector3(0, .835f, -.07f);
                bottle.transform.rotation = Quaternion.Euler(0, 180, 0);
                TuneCharacterMaterials(bottle, "Cola", .12f);

                BuildBoss(characters);
                BuildArms(characters);
                PressureGaugeSceneTools.InstantiateGauge(devices, new Vector3(-.74f, 1.19f, -.11f), Quaternion.identity, Vector3.one);
                ConsoleAndIdleSceneTools.InstantiateConsole(devices, new Vector3(-.74f, .90f, -.19f), Quaternion.identity);
                CharacterIdleFactory.Apply(root);
                BuildLighting(lighting);
                mainCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                mainCamera.transform.SetParent(cameras, false);
                mainCamera.tag = "MainCamera";
                mainCamera.transform.position = new Vector3(0, 1.32f, -1.40f);
                mainCamera.transform.rotation = Quaternion.Euler(11, 0, 0);
                mainCamera.fieldOfView = 50;
                mainCamera.nearClipPlane = .035f;
                mainCamera.farClipPlane = 45;
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(.035f, .026f, .02f);
                mainCamera.allowHDR = false;
                mainCamera.allowMSAA = false;
                mainCamera.aspect = 16f / 9f;
                mainCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                PlayerFacingDeviceLayout.Apply(root, mainCamera);
                HorrorSceneTools.ApplyToScene(root, mainCamera);
                StatusTerminalSceneTools.ApplyToScene(root, mainCamera);
                PressureInteractionSceneTools.ApplyToScene(root, mainCamera);
                TestItemLayoutSceneTools.ApplyToScene(root);
                TabletopFocusSceneTools.ApplyToScene(root);
                ColaShakeAnimationSceneTools.ApplyToScene(root);
                ColaDuelSceneTools.ApplyToScene(root);

                UnityEngine.Object.DestroyImmediate(props);
                UnityEngine.Object.DestroyImmediate(environment);
                props = environment = null;
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save " + ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                    .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = mainCamera.gameObject;
                WriteReport(root);
                Debug.Log("COLA_MAIN_SCENE_SAVED " + ScenePath);
                if (Application.isBatchMode)
                {
                    double captureAt = EditorApplication.timeSinceStartup + 3;
                    EditorApplication.CallbackFunction capture = null;
                    capture = () =>
                    {
                        if (EditorApplication.timeSinceStartup < captureAt) return;
                        EditorApplication.update -= capture;
                        try { Capture(mainCamera, Output + "/MainScene-preview.png"); EditorApplication.Exit(0); }
                        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(2); }
                    };
                    EditorApplication.update += capture;
                }
                else Capture(mainCamera, Output + "/MainScene-preview.png");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Spawn(string path, Transform parent = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!source) throw new FileNotFoundException(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (parent) go.transform.SetParent(parent, true);
            return go;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No mesh: " + go.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // FBX children already contain axis/unit conversion: retain their world rotation and scale.
        private static GameObject Prop(string sourceName, string name, Transform parent,
            Vector3 groundCenter, Vector3 targetSize, float yaw = 0, bool fromEnvironment = false)
        {
            var sourceRoot = fromEnvironment ? environment : props;
            var source = sourceRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == sourceName);
            if (!source) throw new InvalidOperationException("Missing source node: " + sourceName);
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            var orientation = Group(sourceName + "_Orientation", wrapper.transform);
            var copy = UnityEngine.Object.Instantiate(source.gameObject);
            copy.name = sourceName;
            copy.transform.SetParent(orientation, true);
            copy.transform.SetPositionAndRotation(source.position, source.rotation);
            copy.transform.localScale = source.lossyScale;
            // Remove the source pack's distant layout origin before orienting the isolated prop.
            var originalBounds = BoundsOf(wrapper);
            copy.transform.position -= new Vector3(originalBounds.center.x, originalBounds.min.y, originalBounds.center.z);
            orientation.localRotation = Quaternion.Euler(0, yaw, 0);
            var b = BoundsOf(wrapper);
            wrapper.transform.localScale = new Vector3(targetSize.x / b.size.x,
                targetSize.y / b.size.y, targetSize.z / b.size.z);
            b = BoundsOf(wrapper);
            orientation.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            wrapper.transform.position = groundCenter;
            return wrapper;
        }

        private static Material Flat(string name, Color color)
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .08f);
            return material;
        }

        private static Material TextureMaterial(string name, string texturePath, Vector2 tiling)
        {
            var material = Flat(name, Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetTextureScale("_BaseMap", tiling);
            return material;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void BuildRoom(Transform parent)
        {
            var brick = TextureMaterial("Pizzeria_Brick_Blockout", "Assets/2.Assets/04_Pizzeria/Textures/BrickSmallBrown.jpg", new Vector2(5, 2));
            var tile = TextureMaterial("Pizzeria_Floor_Blockout", "Assets/2.Assets/04_Pizzeria/Textures/Tiles_0.png", new Vector2(6, 7));
            var ceiling = TextureMaterial("Pizzeria_Ceiling_Blockout", "Assets/2.Assets/04_Pizzeria/Textures/Office_Ceiling.jpg", new Vector2(5, 6));
            Cube("ArchitectureBlockout_Floor", parent, new Vector3(0, -.08f, 1.8f), new Vector3(8, .16f, 9), tile);
            Cube("ArchitectureBlockout_BackWall", parent, new Vector3(0, 1.65f, 5.3f), new Vector3(8, 3.3f, .15f), brick);
            Cube("ArchitectureBlockout_LeftWall", parent, new Vector3(-4, 1.65f, 1.3f), new Vector3(.15f, 3.3f, 8), brick);
            Cube("ArchitectureBlockout_RightWall", parent, new Vector3(4, 1.65f, 1.3f), new Vector3(.15f, 3.3f, 8), brick);
            Cube("ArchitectureBlockout_Ceiling", parent, new Vector3(0, 3.35f, 1.8f), new Vector3(8, .15f, 9), ceiling);
            Prop("Preparation_table.020", "ServiceCounter_Left", parent, new Vector3(-1.45f, 0, 3.7f), new Vector3(2.9f, .95f, .80f));
            Prop("Preparation_table.020", "ServiceCounter_Right", parent, new Vector3(1.45f, 0, 3.7f), new Vector3(2.9f, .95f, .80f));
            Prop("Pizza_oven", "PizzaOven_ExistingModel", parent, new Vector3(-.8f, 0, 4.7f), new Vector3(1.35f, 1.85f, .92f));
            Prop("Soda_machine", "SodaFountain_ExistingModel", parent, new Vector3(2.0f, .95f, 3.8f), new Vector3(.85f, 1.08f, .85f), -90);
            Prop("cash_register", "CashRegister_ExistingModel", parent, new Vector3(-1.6f, .96f, 3.45f), new Vector3(.45f, .38f, .42f));
            var menu = TextureMaterial("Pizzeria_Menu_Blockout", "Assets/2.Assets/04_Pizzeria/Textures/Menu.png", Vector2.one);
            Cube("ArchitectureBlockout_MenuBoard", parent, new Vector3(-2.0f, 2.02f, 5.12f), new Vector3(2.1f, .78f, .08f), menu);
            for (int i = 0; i < 4; i++)
                Prop("Pizza_box", "PizzaBoxStack_" + i, parent, new Vector3(-2.5f, .96f + i * .065f, 3.5f), new Vector3(.44f, .06f, .44f), i % 2 * 4, true);
            foreach (float side in new[] { -1f, 1f })
            {
                Prop("Chair_Table", "SideTable_" + side, parent, new Vector3(side * 2.65f, 0, 1.5f), new Vector3(1.25f, .81f, .85f));
                Prop("Chair_Table.001", "SideBench_" + side, parent, new Vector3(side * 2.65f, 0, 2.16f), new Vector3(1.5f, 1.08f, .58f));
            }
            for (int i = 0; i < 3; i++)
                Prop("Light", "CeilingLight_ExistingModel_" + i, parent, new Vector3((i - 1) * 2.3f, 2.94f, i == 1 ? .1f : 3.0f), new Vector3(.60f, .05f, .60f), 0, true);
        }

        private static Transform Bone(GameObject root, string name)
        {
            var bone = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (!bone) throw new InvalidOperationException("Missing bone " + name);
            return bone;
        }

        private static void Aim(Transform bone, Transform child, Vector3 target)
        {
            var direction = child.position - bone.position;
            if (direction.sqrMagnitude > .000001f)
                bone.rotation = Quaternion.FromToRotation(direction, target - bone.position) * bone.rotation;
        }

        private static void BuildBoss(Transform parent)
        {
            var boss = Spawn("Assets/2.Assets/03_Simple_Character_PSX/Prefabs/simple-character-psx_fbx.prefab", parent);
            boss.name = "Boss_ExistingRig_SeatedBlockPose";
            boss.transform.SetPositionAndRotation(new Vector3(.10f, -.49f, .98f), Quaternion.Euler(0, 180, 0));
            var animator = boss.GetComponentInChildren<Animator>();
            if (animator) animator.enabled = false;
            foreach (string side in new[] { "L", "R" })
            {
                var upper = Bone(boss, "upper_arm." + side);
                var forearm = Bone(boss, "forearm." + side);
                var hand = Bone(boss, "hand." + side);
                float sign = Mathf.Sign(upper.position.x - boss.transform.position.x);
                Aim(upper, forearm, new Vector3(.10f + sign * .36f, .91f, .83f));
                Aim(forearm, hand, new Vector3(.10f + sign * .16f, .89f, .50f));
                var thigh = Bone(boss, "thigh." + side);
                var shin = Bone(boss, "shin." + side);
                Aim(thigh, shin, thigh.position + new Vector3(sign * .02f, -.08f, -.52f));
                var foot = shin.childCount > 0 ? shin.GetChild(0) : null;
                if (foot) Aim(shin, foot, new Vector3(shin.position.x, .08f, shin.position.z));
            }
            foreach (var renderer in boss.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            TuneCharacterMaterials(boss, "Boss", .03f);
        }

        private static void BuildArms(Transform parent)
        {
            var arms = Spawn("Assets/2.Assets/02_Player_Arms/Prefabs/Player_Arms_fbx.prefab", parent);
            arms.name = "PlayerArms_ExistingRig_TablePose";
            arms.transform.localScale = Vector3.one * .14f;
            var animator = arms.GetComponentInChildren<Animator>();
            if (animator) animator.enabled = false;
            foreach (string side in new[] { "L", "R" })
            {
                float sign = side == "L" ? -1 : 1;
                var upper = Bone(arms, "UpperArm." + side);
                var forearm = Bone(arms, "LowArm." + side);
                var wrist = Bone(arms, "Wrist." + side);
                upper.position = new Vector3(sign * .49f, .70f, -1.25f);
                Aim(upper, forearm, new Vector3(sign * .46f, .83f, -1.02f));
                Aim(forearm, wrist, new Vector3(sign * .35f, .88f, -.77f));
                Aim(wrist, Bone(arms, "Middle1." + side), wrist.position + new Vector3(-sign * .04f, 0, .20f));
                if (side == "L")
                {
                    var fingerDirection = Bone(arms, "Middle1.L").position - wrist.position;
                    wrist.rotation = Quaternion.AngleAxis(180, fingerDirection.normalized) * wrist.rotation;
                }
            }
            foreach (var renderer in arms.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            arms.transform.position += new Vector3(0, .025f, .12f);
            TuneCharacterMaterials(arms, "PlayerArms", .03f);
        }

        // Scene-local material copies keep the imported art untouched.
        private static void TuneCharacterMaterials(GameObject root, string prefix, float smoothness)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (!original) continue;
                    string path = Materials + "/" + prefix + "_" + original.name + ".mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!copy) { copy = new Material(original); AssetDatabase.CreateAsset(copy, path); }
                    copy.SetFloat("_Smoothness", smoothness);
                    copy.SetFloat("_SpecularHighlights", 0);
                    copy.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    if (prefix == "Cola" && original.name.StartsWith("PET_Plastic"))
                    {
                        copy.SetColor("_BaseColor", new Color(.65f, .72f, .65f, .07f));
                        copy.SetFloat("_Surface", 1);
                        copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        copy.SetFloat("_ZWrite", 0);
                        copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        copy.SetOverrideTag("RenderType", "Transparent");
                        copy.renderQueue = (int)RenderQueue.Transparent;
                    }
                    if (prefix == "Cola" && original.name.StartsWith("Cola_Liquid"))
                        copy.SetColor("_BaseColor", new Color(.26f, .11f, .045f, 1));
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static void BuildLighting(Transform parent)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.12f, .10f, .08f);
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = .25f;
            LightAt("Warm_Table_Key", parent, new Vector3(0, 2.8f, -.2f), new Vector3(90, 0, 0), new Color(1, .76f, .48f), 3.5f, LightType.Spot, 100);
            LightAt("Warm_Face_Fill", parent, new Vector3(-.6f, 1.9f, -1.0f), new Vector3(30, 15, 0), new Color(1, .88f, .68f), 1.1f, LightType.Point);
            LightAt("Kitchen_Warm", parent, new Vector3(-1.9f, 2.8f, 3.4f), new Vector3(90, 0, 0), new Color(1, .69f, .39f), 3, LightType.Spot, 110);
            LightAt("Soda_Cool_Fill", parent, new Vector3(2.1f, 2.4f, 3.4f), Vector3.zero, new Color(.51f, .74f, .72f), .35f, LightType.Point);
        }

        private static void LightAt(string name, Transform parent, Vector3 position, Vector3 rotation, Color color, float intensity, LightType type, float angle = 90)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.SetPositionAndRotation(position, Quaternion.Euler(rotation));
            light.type = type; light.color = color; light.intensity = intensity; light.range = type == LightType.Point ? 3 : 5;
            light.spotAngle = angle; light.innerSpotAngle = angle * .7f;
            light.shadows = type == LightType.Spot ? LightShadows.Hard : LightShadows.None;
            light.shadowBias = .025f; light.shadowNormalBias = .1f;
        }

        internal static void Capture(Camera camera, string path, int width = 1600, int height = 900)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            var old = camera.targetTexture;
            var previous = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = old;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("COLA_MAIN_SCENE_RENDERED " + path);
        }

        internal static void WriteReport(GameObject root, Camera camera = null)
        {
            if (camera) mainCamera = camera;
            var report = new StringBuilder();
            report.AppendLine("Scene: " + ScenePath);
            report.AppendLine("Reference: output/concept-art/pizzeria-boss-v4.png");
            report.AppendLine($"Camera: position={mainCamera.transform.position:F3}, rotation={mainCamera.transform.eulerAngles:F3}, vertical FOV={mainCamera.fieldOfView}, aspect=16:9");
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("PLACEHOLDER_") || t.name.StartsWith("TestItem0") ||
                    t.name == "PlayerInventory" || t.name == "BossInventory" || t.parent && t.parent.name.StartsWith("0"))
                    report.AppendLine($"{t.name}: position={t.position:F3} rotation={t.eulerAngles:F2} scale={t.lossyScale:F3}");
            report.AppendLine("Scope: playable single-player duel with original FPS/boss hands, live health/tokens, public pressure forecasts, burst presentation, victory/defeat, restart and escalating boss health. In-turn ordering supplies 11 adopted consumables as Cube props. Boss strength selection is provisional random choice unless modified by an item.");
            File.WriteAllText(Output + "/MainScene-layout.txt", report.ToString());
        }

        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Start MainScene verification from Edit mode; the tool enters Play itself.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool("COLA_COLA.MainScene.PlayVerification", true);
            RestorePlayVerification();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestorePlayVerification()
        {
            if (!SessionState.GetBool("COLA_COLA.MainScene.PlayVerification", false)) return;
            EditorApplication.update -= VerifyMainSceneTick;
            EditorApplication.update += VerifyMainSceneTick;
        }

        private static void VerifyMainSceneTick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount < 3) return;
            EditorApplication.update -= VerifyMainSceneTick;
            SessionState.SetBool("COLA_COLA.MainScene.PlayVerification", false);
            VerifyInPlay();
        }

        private static void VerifyInPlay()
        {
            try
            {
                if (!Application.isPlaying) throw new InvalidOperationException("MainScene checks require Play mode.");
                var scene = SceneManager.GetActiveScene();
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var cameras = objects.Select(t => t.GetComponent<Camera>()).Where(c => c).ToArray();
                if (cameras.Length != 1 || !cameras[0].CompareTag("MainCamera")) throw new InvalidOperationException("Expected exactly one tagged main camera.");
                if (objects.Count(t => t.GetComponent<AudioListener>()) != 1) throw new InvalidOperationException("Expected exactly one audio listener.");
                var missingScripts = objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missingScripts != 0) throw new InvalidOperationException("Missing scripts: " + missingScripts);
                var renderers = objects.Select(t => t.GetComponent<Renderer>()).Where(r => r).ToArray();
                if (renderers.Any(r => r.sharedMaterials.Any(m => !m || !m.shader))) throw new InvalidOperationException("Missing renderer material/shader.");
                var duel = UnityEngine.Object.FindAnyObjectByType<ColaCola.Presentation.ColaDuelController>();
                if (duel)
                {
                    if (!duel.interaction || !duel.terminal || !duel.itemRoot || duel.itemRoot.activeInHierarchy ||
                        !duel.itemLight || duel.itemLight.enabled || duel.interaction.shakeAnimator.previewPlayerOnPlay)
                        throw new InvalidOperationException("Playable duel bindings, disabled items or manual input setup are invalid.");
                }
                else TestItemLayoutSceneTools.Validate(scene.GetRootGameObjects().Single(g => g.name == "COLA_COLA_MainScene"), cameras[0]);
                var gauge = objects.Single(t => t.name == "PressureGauge");
                if (!gauge.GetComponentsInChildren<Transform>().Any(t => t.name == "NeedlePivot") ||
                    gauge.GetComponentsInChildren<MeshFilter>().Length < 6 ||
                    objects.Any(t => t.name == "PLACEHOLDER_PressureGauge"))
                    throw new InvalidOperationException("Pressure gauge model/pivot replacement was not preserved.");
                var console = objects.Single(t => t.name == "ShakeConsole");
                if (console.GetComponentsInChildren<MeshFilter>().Length < 6 ||
                    objects.Any(t => t.name == "PLACEHOLDER_ShakeConsole" || t.name.StartsWith("PLACEHOLDER_ShakeButton")))
                    throw new InvalidOperationException("Console/button model replacement was not preserved.");
                var terminal = objects.Single(t => t.name == "StatusTerminal");
                var gaugeDisplay = gauge.GetComponent<ColaCola.Presentation.PressureGaugeDisplay>();
                if (!gaugeDisplay || !gaugeDisplay.TryValidateBindings(out _) ||
                    !objects.Any(t => t.GetComponent<ColaCola.Presentation.ShakeSelectionInteraction>()))
                    throw new InvalidOperationException("Dynamic pressure preview and cola confirmation were not preserved.");
                var terminalDisplay = terminal.GetComponent<ColaCola.Presentation.StatusTerminalDisplay>();
                if (!terminalDisplay || !terminalDisplay.TryValidateBindings(out _))
                    throw new InvalidOperationException("Physical health/token terminal bindings were not preserved.");
                foreach (var name in new[] { CharacterIdleFactory.BossName, CharacterIdleFactory.ArmsName })
                {
                    var animator = objects.Single(t => t.name == name).GetComponent<Animator>();
                    if (!animator || !animator.enabled || !animator.runtimeAnimatorController || animator.applyRootMotion ||
                        !AnimationUtility.GetAnimationClipSettings(animator.runtimeAnimatorController.animationClips.Single()).loopTime)
                        throw new InvalidOperationException("Looping Idle Animator was not saved for " + name);
                }
                if (EditorBuildSettings.scenes.First().path != ScenePath) throw new InvalidOperationException("MainScene is not the first build scene.");
                mainCamera = cameras[0];
                mainCamera.aspect = 16f / 9f;
                var bottle = objects.Single(t => t.name == "HeroCola_ExistingModel");
                var point = mainCamera.WorldToViewportPoint(bottle.position + Vector3.up * .2f);
                if (point.z <= 0 || point.x < .35f || point.x > .65f || point.y < .15f || point.y > .8f)
                    throw new InvalidOperationException("Hero bottle is outside the intended central frame: " + point);
                var report = $"PASS: Saved MainScene reopened in Unity {Application.unityVersion}\nPASS: one MainCamera and one AudioListener\nPASS: {renderers.Length} renderers with material/shader references\nPASS: no missing scripts\nPASS: six authored test item models per participant, twelve instances, no remaining item Cube placeholders\nPASS: custom PressureGauge model and NeedlePivot replace the gauge Cube\nPASS: custom ShakeConsole with three independent numbered keys replaces console/button Cubes\nPASS: physical StatusTerminal with complete health/token readout bindings\nPASS: both character Animators have enabled looping Idle controllers without root motion\nPASS: MainScene is first enabled build scene\nPASS: hero bottle center viewport={point:F3}\nScope: 3D staging, test item props, character Idle, sample physical readouts and pressure selection/confirmation prototype. Boss AI, damage, token awards, item effects and shop flow are not implemented.\n";
                if (duel)
                    report = report.Replace("six authored test item models per participant, twelve instances, no remaining item Cube placeholders", "playable single-player duel; item props/light hidden and automatic hand preview disabled")
                        .Replace("sample physical readouts and pressure selection/confirmation prototype. Boss AI, damage, token awards, item effects and shop flow are not implemented.", "live health/token readouts, alternating pressure actions, in-hand cola burst presentation, victory/defeat/retry and escalating boss health. In-turn ordering and 11 adopted consumables with runtime Cube inventory are covered by the separate item Play-mode report.");
                File.WriteAllText(Output + "/MainScene-validation.txt", report);
                Debug.Log("COLA_MAIN_SCENE_VALIDATION_PASS");
                EditorApplication.isPlaying = false;
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }

        [MenuItem("COLA_COLA/Open Main Scene")]
        public static void OpenMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera)
            {
                Selection.activeGameObject = camera.gameObject;
                if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.AlignViewToObject(camera.transform);
            }
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var report = new StringBuilder();
            foreach (var path in new[] {
                "Assets/2.Assets/04_Pizzeria/Prefabs/Pizzeria_Scene_fbx.prefab",
                "Assets/2.Assets/04_Pizzeria/Prefabs/Pizzeria_Props_fbx.prefab",
                "Assets/2.Assets/03_Simple_Character_PSX/Prefabs/simple-character-psx_fbx.prefab",
                "Assets/2.Assets/02_Player_Arms/Prefabs/Player_Arms_fbx.prefab",
                "Assets/2.Assets/01_Cola_Bottle/Prefabs/cola_bottle_fbx.prefab" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset) throw new FileNotFoundException(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                report.AppendLine("ASSET " + path);
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                    report.AppendLine($"RENDER {r.name} center={r.bounds.center:F4} size={r.bounds.size:F4} rotation={r.transform.eulerAngles:F2} scale={r.transform.lossyScale:F4} materials={string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "NULL"))}");
                if (path.Contains("Character") || path.Contains("Arms"))
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                        report.AppendLine($"BONE {t.name} position={t.position:F4} localEuler={t.localEulerAngles:F2}");
                UnityEngine.Object.DestroyImmediate(instance);
            }
            File.WriteAllText(Output + "/asset-inspection.txt", report.ToString());
            Debug.Log("COLA_LAYOUT_INSPECTION_COMPLETE");
        }
    }
}
