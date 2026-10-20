using UnityEngine;

namespace ColaCola.Presentation
{
    /// <summary>Keeps the FPS sleeve on one straight axis, with no elbow bend or differential arm twist.</summary>
    internal static class ColaArmPose
    {
        internal static void Straighten(Transform shoulder, Transform elbow, Transform hand,
            Vector3 target, Quaternion orientation, Quaternion restingWristRelativeRotation, ref float continuousRoll)
        {
            float upperLength = Vector3.Distance(shoulder.position, elbow.position);
            float lowerLength = Vector3.Distance(elbow.position, hand.position);
            if (upperLength < .0001f || lowerLength < .0001f) return;
            Vector3 direction = target - shoulder.position;
            if (direction.sqrMagnitude < .000001f) direction = hand.position - elbow.position;
            direction.Normalize();
            shoulder.position = target - direction * (upperLength + lowerLength);
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, direction) * shoulder.rotation;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, direction) * elbow.rotation;
            AlignWrist(shoulder, elbow, hand, orientation, restingWristRelativeRotation, ref continuousRoll);
        }

        internal static void AlignWrist(Transform shoulder, Transform elbow, Transform hand,
            Quaternion orientation, Quaternion restingWristRelativeRotation, ref float continuousRoll)
        {
            Vector3 forearmAxis = hand.position - elbow.position;
            if (forearmAxis.sqrMagnitude < .000001f) { hand.rotation = orientation; return; }
            forearmAxis.Normalize();
            Quaternion forearmRotation = elbow.rotation;
            Quaternion desiredForearm = orientation * Quaternion.Inverse(restingWristRelativeRotation);
            Quaternion difference = desiredForearm * Quaternion.Inverse(forearmRotation);
            if (difference.w < 0)
                difference = new Quaternion(-difference.x, -difference.y, -difference.z, -difference.w);
            float projected = Vector3.Dot(new Vector3(difference.x, difference.y, difference.z), forearmAxis);
            float length = Mathf.Sqrt(projected * projected + difference.w * difference.w);
            if (length > .0001f)
            {
                float wrapped = 2 * Mathf.Atan2(projected / length, difference.w / length) * Mathf.Rad2Deg;
                // Preserve the same continuous axial rotation on both segments; no elbow seam twist.
                continuousRoll += Mathf.DeltaAngle(continuousRoll, wrapped);
                float angle = continuousRoll;
                Vector3 upperAxis = elbow.position - shoulder.position;
                // Both rotations are about their own bone axis: joint positions and bone lengths stay fixed.
                if (upperAxis.sqrMagnitude > .000001f)
                    shoulder.rotation = Quaternion.AngleAxis(angle, upperAxis.normalized) * shoulder.rotation;
                elbow.rotation = Quaternion.AngleAxis(angle, forearmAxis) * forearmRotation;
            }
            hand.rotation = orientation;
        }
    }
}
