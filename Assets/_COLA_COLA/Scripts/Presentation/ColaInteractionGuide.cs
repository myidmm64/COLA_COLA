using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ColaCola.Presentation
{
    /// <summary>Depth-tested mesh silhouettes guide strength selection, then cola confirmation.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(2300)]
    public sealed class ColaInteractionGuide : MonoBehaviour
    {
        [Range(.5f, 10)] public float outlinePixels = 2.5f;
        [Range(.5f, 10)] public float colaOutlinePixels = 2.5f;
        [Range(.5f, 10)] public float menuOutlinePixels = 2.5f;
        private const int MenuTarget = -3;
        private const int BellTarget = -4;
        private Transform outlinedMenu, outlinedBell;
        private ShakeSelectionInteraction interaction;
        private Material outlineMaterial, maskMaterial;
        private readonly List<Edge> edges = new();
        private static readonly Vector2[] OutlineDirections =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(.7071068f, .7071068f), new Vector2(-.7071068f, .7071068f),
            new Vector2(.7071068f, -.7071068f), new Vector2(-.7071068f, -.7071068f)
        };
        private sealed class Edge
        {
            public Renderer source, outline, mask;
            public SkinnedMeshRenderer sourceSkin, outlineSkin, maskSkin;
            public MaterialPropertyBlock properties;
            public int button; // 0..2 = strength, -1 = cola, -3 = resting order sheet
        }

        public static void EnsureFor(ShakeSelectionInteraction owner)
        {
            var guide = owner.GetComponent<ColaInteractionGuide>();
            if (!guide) guide = owner.gameObject.AddComponent<ColaInteractionGuide>();
            guide.interaction = owner;
            if (guide.outlineMaterial && guide.maskMaterial) return;
            var shader = Resources.Load<Shader>("COLA_COLA_InteractionOutline");
            var maskShader = Resources.Load<Shader>("COLA_COLA_InteractionMask");
            if (!shader || !maskShader) return;
            guide.outlineMaterial = new Material(shader) { name = "Gold next-action outline", hideFlags = HideFlags.DontSave };
            guide.maskMaterial = new Material(maskShader) { name = "Next-action interior exclusion", hideFlags = HideFlags.DontSave };
            for (int i = 0; i < owner.strengthButtons.Length; i++) guide.AddTarget(owner.strengthButtons[i], i);
            guide.AddTarget(owner.bottle, -1);
        }

        private void AddTarget(Transform target, int button)
        {
            if (!target) return;
            // Snapshot the filters before adding children so the outline never outlines itself.
            foreach (var filter in target.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.GetComponent<MeshRenderer>();
                if (!source || !filter.sharedMesh) continue;
                if (IsOuterGeometry(source, button)) AddEdge(source, filter.sharedMesh, button);
                else if (button >= 0 && source.name.StartsWith("RaisedNumber"))
                    AddInteriorMask(source, filter.sharedMesh, button);
            }
            // The cola shell is a blend-shape SkinnedMeshRenderer, not a MeshFilter like its cap.
            foreach (var source in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!source.sharedMesh || !IsOuterGeometry(source, button)) continue;
                AddEdge(source, source.sharedMesh, button);
            }
        }

        private static bool IsOuterGeometry(Renderer source, int button)
        {
            if (source.GetComponent<TextMesh>() || source.GetComponent<ColaWorldText>()) return false;
            if (button == BellTarget) return true;
            if (button == MenuTarget) return source.name == "BeveledClipboard";
            // RaisedNumber1/2/3 are meshes too. Explicitly select only the physical keycap.
            if (button >= 0) return source.name == "BeveledKeycap";
            // Share one union mask for shell, cap and label. Only the complete bottle's exterior survives.
            // Label lettering is a texture, so it never becomes separate outlined glyphs.
            return source.name == "Bottle_Shell" || source.name == "Cap" || source.name == "Label";
        }

        private static Renderer CopyGeometry(Renderer source, Mesh mesh, Material material, string name)
        {
            var edge = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
            edge.transform.SetParent(source.transform, false);
            Renderer renderer;
            if (source is SkinnedMeshRenderer skin)
            {
                var copy = edge.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = mesh; copy.bones = skin.bones; copy.rootBone = skin.rootBone;
                copy.localBounds = skin.localBounds; copy.quality = skin.quality; copy.updateWhenOffscreen = true;
                renderer = copy;
            }
            else
            {
                edge.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = edge.AddComponent<MeshRenderer>();
            }
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        private void AddEdge(Renderer source, Mesh mesh, int button)
        {
            var mask = CopyGeometry(source, mesh, maskMaterial, "Next action interior mask");
            for (int i = 0; i < OutlineDirections.Length; i++)
            {
                var renderer = CopyGeometry(source, mesh, outlineMaterial, "Next action silhouette " + i);
                var properties = new MaterialPropertyBlock();
                Vector2 direction = OutlineDirections[i];
                properties.SetVector("_OutlineDirection", new Vector4(direction.x, direction.y, 0, 0));
                properties.SetFloat("_OutlinePixels", WidthFor(button));
                renderer.SetPropertyBlock(properties);
                // Each source owns one shared mask; the neighboring silhouettes only add the outer band.
                edges.Add(new Edge { source = source, outline = renderer, mask = i == 0 ? mask : null, button = button,
                    sourceSkin = source as SkinnedMeshRenderer, outlineSkin = renderer as SkinnedMeshRenderer,
                    maskSkin = i == 0 ? mask as SkinnedMeshRenderer : null, properties = properties });
            }
        }

        private float WidthFor(int target) => target == MenuTarget || target == BellTarget ? menuOutlinePixels : target == -1 ? colaOutlinePixels : outlinePixels;

        private void BindBell(ColaWaiterBell bell)
        {
            if (!bell || !bell.ModelRoot || !outlineMaterial || !maskMaterial || outlinedBell == bell.ModelRoot) return;
            outlinedBell = bell.ModelRoot;
            AddTarget(outlinedBell, BellTarget);
        }

        private void BindMenu(ColaOrderSheet sheet)
        {
            if (!sheet || !sheet.ModelRoot || !outlineMaterial || !maskMaterial || outlinedMenu == sheet.ModelRoot) return;
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                var edge = edges[i];
                if (edge.button != MenuTarget) continue;
                if (edge.outline) Destroy(edge.outline.gameObject);
                if (edge.mask) Destroy(edge.mask.gameObject);
                edges.RemoveAt(i);
            }
            outlinedMenu = sheet.ModelRoot;
            AddTarget(outlinedMenu, MenuTarget);
        }

        private void AddInteriorMask(Renderer source, Mesh mesh, int button)
        {
            var mask = CopyGeometry(source, mesh, maskMaterial, "Next action text exclusion mask");
            edges.Add(new Edge { source = source, mask = mask, button = button,
                sourceSkin = source as SkinnedMeshRenderer, maskSkin = mask as SkinnedMeshRenderer });
        }

        private static void SyncShape(Edge edge)
        {
            if (!edge.sourceSkin) return;
            for (int i = 0; i < edge.sourceSkin.sharedMesh.blendShapeCount; i++)
            {
                if (edge.outlineSkin) edge.outlineSkin.SetBlendShapeWeight(i, edge.sourceSkin.GetBlendShapeWeight(i));
                if (edge.maskSkin) edge.maskSkin.SetBlendShapeWeight(i, edge.sourceSkin.GetBlendShapeWeight(i));
            }
            if (edge.outlineSkin) edge.outlineSkin.localBounds = edge.sourceSkin.localBounds;
            if (edge.maskSkin) edge.maskSkin.localBounds = edge.sourceSkin.localBounds;
        }

        private int HoveredTarget(ColaOrderSheet sheet)
        {
            const int none = -2;
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused || !interaction.playerCamera ||
                (EventSystem.current && EventSystem.current.IsPointerOverGameObject())) return none;
            Vector2 point = mouse.position.ReadValue();
            if (!interaction.playerCamera.pixelRect.Contains(point)) return none;
            // Use the same first-hit ray as clicking, so props and the order sheet also block hover.
            Physics.SyncTransforms();
            if (!Physics.Raycast(interaction.playerCamera.ScreenPointToRay(point), out var hit, 8, ~0,
                    QueryTriggerInteraction.Ignore)) return none;
            var bell = hit.collider.GetComponent<ColaWaiterBellTarget>();
            if (bell && bell.Bell && bell.Bell.CanHover) return BellTarget;
            if (sheet && sheet.IsVisible && sheet.CanOpen && !sheet.BlocksGameplay && hit.collider == sheet.OpenCollider)
                return MenuTarget;
            for (int i = 0; i < interaction.strengthButtons.Length; i++)
            {
                var button = interaction.strengthButtons[i];
                if (button && (hit.transform == button || hit.transform.IsChildOf(button))) return i;
            }
            if (hit.collider == interaction.bottleCollider || hit.transform == interaction.bottle ||
                (interaction.bottle && hit.transform.IsChildOf(interaction.bottle))) return -1;
            return none;
        }

        private void LateUpdate()
        {
            bool interactive = Application.IsPlaying(gameObject) && interaction && interaction.isActiveAndEnabled &&
                interaction.InputEnabled && interaction.Model != null;
            bool available = interactive && !interaction.IsBusy && !interaction.IsBossTurn;
            var sheet = interaction && interaction.items ? interaction.items.GetComponent<ColaOrderSheet>() : null;
            var bell = interaction && interaction.items ? interaction.items.GetComponent<ColaWaiterBell>() : null;
            BindMenu(sheet);
            BindBell(bell);
            if (sheet && sheet.BlocksGameplay) available = interactive = false;
            int hovered = interactive ? HoveredTarget(sheet) : -2;
            bool selected = interaction && interaction.Model != null && interaction.Model.SelectedStrength > 0;
            if (outlineMaterial)
            {
                float breath = .5f + .5f * Mathf.Sin(ColaGameClock.Time * 2.6f);
                outlineMaterial.SetFloat("_OutlinePixels", outlinePixels);
                outlineMaterial.SetColor("_OutlineColor", new Color(.86f, .74f, .13f, Mathf.Lerp(.86f, .96f, breath)));
            }
            foreach (var edge in edges)
            {
                bool targetAvailable = edge.button == BellTarget ? interactive && bell && bell.CanHover : available;
                bool visible = targetAvailable && edge.button == hovered && edge.source && edge.source.enabled && edge.source.gameObject.activeInHierarchy &&
                    (edge.button == BellTarget ? true : edge.button == MenuTarget ? sheet && sheet.IsVisible && sheet.CanOpen && !sheet.BlocksGameplay :
                        edge.button >= 0 || selected);
                if (edge.outline) edge.outline.enabled = visible;
                if (edge.mask) edge.mask.enabled = visible;
                if (visible)
                {
                    if (edge.outline)
                    {
                        edge.properties.SetFloat("_OutlinePixels", WidthFor(edge.button));
                        edge.outline.SetPropertyBlock(edge.properties);
                    }
                    SyncShape(edge);
                }
            }
        }

        private void OnDisable()
        {
            foreach (var edge in edges)
            {
                if (edge.outline) edge.outline.enabled = false;
                if (edge.mask) edge.mask.enabled = false;
            }
        }
        private void OnDestroy()
        {
            foreach (var edge in edges)
            {
                if (edge.outline) Destroy(edge.outline.gameObject);
                if (edge.mask) Destroy(edge.mask.gameObject);
            }
            if (outlineMaterial) Destroy(outlineMaterial);
            if (maskMaterial) Destroy(maskMaterial);
        }
    }
}
