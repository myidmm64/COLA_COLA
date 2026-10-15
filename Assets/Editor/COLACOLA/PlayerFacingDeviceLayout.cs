using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ColaCola.Editor
{
    /// <summary>Turns the complete gauge/selector assembly toward the seated player's camera.</summary>
    public static class PlayerFacingDeviceLayout
    {
        // Both authored prefabs use local -Z as their front. Keep their existing pitch,
        // height and spacing; only yaw the assembly around the console's fixed center.
        public static string Apply(GameObject sceneRoot, Camera playerCamera)
        {
            if (!sceneRoot) throw new ArgumentNullException(nameof(sceneRoot));
            if (!playerCamera) throw new ArgumentNullException(nameof(playerCamera));

            var transforms = sceneRoot.GetComponentsInChildren<Transform>(true);
            var console = FindOne(transforms, "ShakeConsole");
            var gauge = FindOne(transforms, "PressureGauge");
            if (console.IsChildOf(gauge) || gauge.IsChildOf(console))
                throw new InvalidOperationException("Gauge and console must be separate assembly roots.");

            var unaffected = transforms.Where(t => !BelongsTo(t, console) && !BelongsTo(t, gauge))
                .ToDictionary(t => t, t => new Pose(t.position, t.rotation));
            var pivot = console.position;
            var offsetBefore = gauge.position - pivot;
            var consoleRotationBefore = console.rotation;
            var gaugeRotationBefore = gauge.rotation;
            var frontBefore = Horizontal(console.TransformDirection(Vector3.back));
            var target = Horizontal(playerCamera.transform.position - pivot);
            var yawDelta = Vector3.SignedAngle(frontBefore, target, Vector3.up);

            // A second call sees a zero delta. Do not introduce accumulated quaternion
            // rounding or progressively orbit the gauge around the console.
            if (Mathf.Abs(yawDelta) > .0001f)
            {
                var turn = Quaternion.AngleAxis(yawDelta, Vector3.up);
                console.rotation = turn * consoleRotationBefore;
                gauge.SetPositionAndRotation(pivot + turn * offsetBefore, turn * gaugeRotationBefore);
                PrefabUtility.RecordPrefabInstancePropertyModifications(console);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gauge);
            }

            var frontAfter = Horizontal(console.TransformDirection(Vector3.back));
            var gaugeFront = Horizontal(gauge.TransformDirection(Vector3.back));
            var gaugeToPlayer = Horizontal(playerCamera.transform.position - gauge.position);
            var consoleDot = Vector3.Dot(frontAfter, target);
            var gaugeDot = Vector3.Dot(gaugeFront, gaugeToPlayer);
            if (consoleDot < .99999f || gaugeDot < .98f)
                throw new InvalidOperationException("The pressure console did not face the player.");
            if (Vector3.Distance(console.position, pivot) > .00001f ||
                Mathf.Abs((gauge.position - pivot).magnitude - offsetBefore.magnitude) > .00001f ||
                Mathf.Abs(gauge.position.y - pivot.y - offsetBefore.y) > .00001f)
                throw new InvalidOperationException("Device spacing or mounting height changed.");
            foreach (var entry in unaffected)
                if (Vector3.Distance(entry.Key.position, entry.Value.position) > .00001f ||
                    Quaternion.Angle(entry.Key.rotation, entry.Value.rotation) > .01f)
                    throw new InvalidOperationException("An unrelated transform changed during device alignment.");

            var report = new StringBuilder();
            report.AppendLine("PASS: gauge and all three numbered keys face the player's camera in the horizontal plane.");
            report.AppendLine("PASS: console center, mounting height and gauge/console spacing retained.");
            report.AppendLine("PASS: " + unaffected.Count + " unrelated transforms retained their world pose.");
            report.AppendLine("Console pivot: " + pivot.ToString("F4"));
            report.AppendLine("Camera position: " + playerCamera.transform.position.ToString("F4"));
            report.AppendLine("Yaw before / after / applied delta (degrees): " +
                              Format(Yaw(frontBefore)) + " / " + Format(Yaw(frontAfter)) + " / " + Format(yawDelta));
            report.AppendLine("Console front dot camera direction: " + Format(consoleDot));
            report.AppendLine("Gauge front dot camera direction: " + Format(gaugeDot));
            report.AppendLine("Front convention: local -Z; horizontal orientation only, with existing top slope retained.");
            AppendButtonApproachReport(report, transforms, console);
            return report.ToString();
        }

        /// <summary>World rotation for a -Z-facing device, without pitch or roll.</summary>
        public static Quaternion FacingRotation(Vector3 consolePosition, Vector3 viewerPosition)
        {
            return Quaternion.LookRotation(-Horizontal(viewerPosition - consolePosition), Vector3.up);
        }

        private static Transform FindOne(IEnumerable<Transform> transforms, string name)
        {
            var matching = transforms.Where(t => t.name == name).ToArray();
            if (matching.Length != 1)
                throw new InvalidOperationException("Expected one " + name + "; found " + matching.Length + ".");
            return matching[0];
        }

        private static bool BelongsTo(Transform value, Transform root)
        {
            return value == root || value.IsChildOf(root);
        }

        private static Vector3 Horizontal(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < .000001f)
                throw new InvalidOperationException("Device front and player direction require a horizontal component.");
            return direction.normalized;
        }

        private static float Yaw(Vector3 front)
        {
            return Vector3.SignedAngle(Vector3.back, front, Vector3.up);
        }

        private static string Format(float value)
        {
            return value.ToString("F5", CultureInfo.InvariantCulture);
        }

        private static Bounds RendererBounds(Transform transform)
        {
            var renderers = transform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Expected rendered geometry below " + transform.name + ".");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Bounds ToConsoleBounds(Bounds worldBounds, Transform console)
        {
            var local = new Bounds(console.InverseTransformPoint(worldBounds.min), Vector3.zero);
            for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                    for (var z = 0; z < 2; z++)
                        local.Encapsulate(console.InverseTransformPoint(new Vector3(
                            x == 0 ? worldBounds.min.x : worldBounds.max.x,
                            y == 0 ? worldBounds.min.y : worldBounds.max.y,
                            z == 0 ? worldBounds.min.z : worldBounds.max.z)));
            return local;
        }

        private static void AppendButtonApproachReport(StringBuilder report, Transform[] transforms, Transform console)
        {
            // Conservative bounds check for nearby placeholder props. The corridor is
            // the 18 cm immediately in front of each key, at its pressing-surface height.
            // This is a layout aid; it neither moves props nor adds gameplay interaction.
            var props = transforms.Where(t => t.name.StartsWith("PLACEHOLDER_", StringComparison.Ordinal) &&
                                             t.GetComponentsInChildren<Renderer>(true).Length > 0).ToArray();
            var obstructionCount = 0;
            for (var number = 1; number <= 3; number++)
            {
                var button = FindOne(console.GetComponentsInChildren<Transform>(true), "ShakeButton" + number);
                var buttonWorld = RendererBounds(button);
                var buttonLocal = ToConsoleBounds(buttonWorld, console);
                var nearby = 0;
                foreach (var prop in props)
                {
                    var world = RendererBounds(prop);
                    var local = ToConsoleBounds(world, console);
                    var overlapsWidth = local.min.x < buttonLocal.max.x + .012f &&
                                        local.max.x > buttonLocal.min.x - .012f;
                    var overlapsApproach = local.min.z < buttonLocal.min.z &&
                                           local.max.z > buttonLocal.min.z - .18f;
                    if (!overlapsWidth || !overlapsApproach) continue;
                    nearby++;
                    var clearance = buttonWorld.max.y - world.max.y;
                    var blocked = clearance < .015f;
                    if (blocked) obstructionCount++;
                    report.AppendLine((blocked ? "REVIEW" : "PASS") + ": key " + number + " approach near " + prop.name +
                                      ", prop top below key surface by " + Format(clearance) + " m." +
                                      (blocked ? " Consider moving this prop outward before adding hand interaction." : ""));
                }
                if (nearby == 0) report.AppendLine("PASS: key " + number + " approach has no adjacent placeholder prop.");
            }
            report.AppendLine("Nearby props were not moved. Conservative approach warnings: " + obstructionCount + ".");
        }
    }
}
