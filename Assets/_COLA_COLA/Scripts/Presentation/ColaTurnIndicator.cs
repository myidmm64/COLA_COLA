using UnityEngine;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>Player-facing olive pilot lamp with one soft brightness swell on turn arrival.</summary>
    [DisallowMultipleComponent]
    public sealed class ColaTurnIndicator : MonoBehaviour
    {
        private ShakeSelectionInteraction owner;
        private GameObject root;
        private Material bezelMaterial, lensMaterial;
        private Mesh mesh;
        private float brightness;
        private bool previousPlayerTurn, arrivalPending;
        private int previousBottle = -1;
        private float arrivalAt = float.NegativeInfinity;
        public float Brightness => brightness;
        public float ArrivalPulseWeight { get; private set; }
        public Transform ModelRoot => root ? root.transform : null;
        private static readonly Color Off = new(.065f, .080f, .050f);
        private static readonly Color On = new(.56f, .61f, .34f);
        private static readonly Color Arrival = new(.83f, .86f, .49f);

        public static void EnsureFor(ShakeSelectionInteraction interaction)
        {
            if (!interaction || !Application.IsPlaying(interaction.gameObject)) return;
            var indicator = interaction.GetComponent<ColaTurnIndicator>();
            if (!indicator) indicator = interaction.gameObject.AddComponent<ColaTurnIndicator>();
            indicator.owner = interaction;
            indicator.Build();
            indicator.ResetSignal();
        }

        private void Build()
        {
            if (root || owner.strengthButtons == null || owner.strengthButtons.Length < 2 || !owner.strengthButtons[1]) return;
            var console = owner.strengthButtons[1].parent;
            if (!console) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (!lit || !unlit) return;
            bezelMaterial = new Material(lit) { name = "Turn pilot / dark bezel", hideFlags = HideFlags.DontSave };
            bezelMaterial.SetColor("_BaseColor", new Color(.035f, .045f, .038f));
            bezelMaterial.SetFloat("_Smoothness", .08f);
            bezelMaterial.SetFloat("_SpecularHighlights", 0);
            bezelMaterial.SetFloat("_EnvironmentReflections", 0);
            ColaPropPatina.Configure(bezelMaterial);
            lensMaterial = new Material(unlit) { name = "Turn pilot / muted olive lens", hideFlags = HideFlags.DontSave };
            lensMaterial.SetColor("_BaseColor", Off);
            mesh = MakePuck();
            root = new GameObject("PlayerTurn_PilotLamp_Runtime") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(console, false);
            // Vertical near side of the gauge's console, facing the seated player (-local Z).
            // The enlarged lens sits below the keys and projects out of the housing face.
            root.transform.localPosition = new Vector3(0, -.015f, -.1515f);
            root.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            Part("Beveled bezel", Vector3.zero, new Vector3(.104f, .006f, .042f), bezelMaterial);
            Part("Olive pilot lens", Vector3.up * .0061f, new Vector3(.084f, .002f, .026f), lensMaterial);
        }

        private void Part(string label, Vector3 position, Vector3 scale, Material material)
        {
            var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave };
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh MakePuck()
        {
            var outline = new[] { new Vector2(-.38f,-.5f), new Vector2(.38f,-.5f), new Vector2(.5f,-.38f),
                new Vector2(.5f,.38f), new Vector2(.38f,.5f), new Vector2(-.38f,.5f),
                new Vector2(-.5f,.38f), new Vector2(-.5f,-.38f) };
            var vertices = new Vector3[16];
            var triangles = new int[84];
            for (int i = 0; i < 8; i++)
            {
                vertices[i] = new Vector3(outline[i].x, 0, outline[i].y);
                vertices[i+8] = new Vector3(outline[i].x, 1, outline[i].y);
            }
            int at = 0;
            for (int i = 1; i < 7; i++)
            {
                triangles[at++] = 0; triangles[at++] = i; triangles[at++] = i+1;
                triangles[at++] = 8; triangles[at++] = i+9; triangles[at++] = i+8;
            }
            for (int i = 0; i < 8; i++)
            {
                int next = (i+1)%8;
                triangles[at++] = i; triangles[at++] = i+8; triangles[at++] = next+8;
                triangles[at++] = i; triangles[at++] = next+8; triangles[at++] = next;
            }
            var result = new Mesh { name = "Beveled pilot lamp", hideFlags = HideFlags.DontSave };
            result.vertices = vertices; result.triangles = triangles;
            result.RecalculateNormals(); result.RecalculateBounds();
            return result;
        }

        private void LateUpdate()
        {
            if (!root || !lensMaterial) return;
            bool playerTurn = owner && owner.isActiveAndEnabled && owner.InputEnabled && owner.Model != null && !owner.IsBossTurn;
            int bottleNumber = owner ? owner.BottleNumber : -1;
            if (playerTurn && (!previousPlayerTurn || bottleNumber != previousBottle)) arrivalPending = true;
            if (!playerTurn) arrivalPending = false;
            if (arrivalPending && !owner.IsBusy)
            {
                arrivalAt = Time.unscaledTime;
                arrivalPending = false;
            }
            previousPlayerTurn = playerTurn;
            previousBottle = bottleNumber;
            float target = playerTurn ? 1 : 0;
            brightness = Mathf.MoveTowards(brightness, target, Time.unscaledDeltaTime / .28f);
            float eased = brightness * brightness * (3 - 2 * brightness);
            float phase = (Time.unscaledTime - arrivalAt) / .65f;
            float wave = phase >= 0 && phase <= 1 && playerTurn ? Mathf.Sin(phase * Mathf.PI) : 0;
            ArrivalPulseWeight = wave * wave;
            lensMaterial.SetColor("_BaseColor", Color.Lerp(Off, Color.Lerp(On, Arrival, ArrivalPulseWeight), eased));
        }

        private void ResetSignal()
        {
            brightness = 0;
            ArrivalPulseWeight = 0;
            previousPlayerTurn = arrivalPending = false;
            previousBottle = -1;
            arrivalAt = float.NegativeInfinity;
            if (lensMaterial) lensMaterial.SetColor("_BaseColor", Off);
        }
        private void OnDisable() => ResetSignal();
        private void OnDestroy()
        {
            if (root) Destroy(root);
            if (mesh) Destroy(mesh);
            if (bezelMaterial) Destroy(bezelMaterial);
            if (lensMaterial) Destroy(lensMaterial);
        }
    }
}
