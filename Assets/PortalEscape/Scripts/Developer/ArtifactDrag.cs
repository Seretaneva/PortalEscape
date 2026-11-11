using UnityEngine;

namespace PortalEscape
{
    internal sealed class ArtifactDrag
    {
        private const float FollowSpeed = 28f;

        private readonly Vector3 grabPointLocal;
        private readonly Pose originalLocalPose;
        private readonly Quaternion originalRotation;

        internal PlacedArtifact Artifact { get; }
        internal bool HasDestination { get; private set; }
        internal Pose Destination { get; private set; }

        internal ArtifactDrag(PlacedArtifact artifact, Vector3 grabPoint)
        {
            Artifact = artifact;
            var transform = artifact.transform;
            grabPointLocal = transform.InverseTransformPoint(grabPoint);
            originalLocalPose = new Pose(transform.localPosition, transform.localRotation);
            originalRotation = transform.rotation;
        }

        internal void Update(Vector2 finger, Camera camera, ArtifactSurface surface, float deltaTime)
        {
            if (Artifact == null)
                return;

            var transform = Artifact.transform;
            Vector3 originOnScreen = camera.WorldToScreenPoint(transform.position);
            Vector3 grabOnScreen = camera.WorldToScreenPoint(transform.TransformPoint(grabPointLocal));
            if (originOnScreen.z <= 0f || grabOnScreen.z <= 0f)
                return;

            // Cast where the base should land, keeping the picked point under the finger.
            Vector2 targetPoint = finger + (Vector2)(originOnScreen - grabOnScreen);
            if (!camera.pixelRect.Contains(targetPoint) || !surface.TryGetPose(targetPoint, out var hit))
                return;

            Quaternion alignToSurface = Quaternion.FromToRotation(originalRotation * Vector3.up, hit.up);
            Destination = new Pose(hit.position, alignToSurface * originalRotation);
            HasDestination = true;

            float blend = 1f - Mathf.Exp(-FollowSpeed * deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, Destination.position, blend),
                Quaternion.Slerp(transform.rotation, Destination.rotation, blend));
        }

        internal void Restore()
        {
            if (Artifact != null)
                Artifact.transform.SetLocalPositionAndRotation(originalLocalPose.position, originalLocalPose.rotation);
        }
    }
}
