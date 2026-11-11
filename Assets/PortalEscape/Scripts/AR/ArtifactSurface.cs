using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PortalEscape
{
    internal sealed class ArtifactSurface
    {
        private readonly ARRaycastManager raycasts;
        private readonly ARAnchorManager anchors;
        private readonly List<ARRaycastHit> hits = new();

        internal ArtifactSurface(ARRaycastManager raycasts, ARAnchorManager anchors)
        {
            this.raycasts = raycasts;
            this.anchors = anchors;
        }

        internal bool TryGetPose(Vector2 screenPoint, out Pose pose)
        {
            pose = default;
            if (ARSession.state != ARSessionState.SessionTracking ||
                !raycasts.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
                return false;

            pose = hits[0].pose;
            // Fix the heading against the session axes while keeping local up normal to the plane.
            Vector3 normal = pose.up;
            Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.right, normal);
            pose.rotation = Quaternion.LookRotation(forward, normal);
            return true;
        }

        internal async Awaitable<ARAnchor> CreateAnchor(Pose pose)
        {
            if (!anchors || !anchors.isActiveAndEnabled || anchors.subsystem == null || !anchors.subsystem.running)
                return null;

            var result = await anchors.TryAddAnchorAsync(pose);
            if (result.status.IsSuccess())
                return result.value;

            Debug.LogWarning($"Portal Escape: anchor creation failed ({result.status}).");
            return null;
        }

        internal void RemoveAnchor(ARAnchor anchor)
        {
            if (anchor == null)
                return;

            // ARAnchor.OnDisable removes the provider anchor through its manager.
            Object.Destroy(anchor.gameObject);
        }
    }
}
