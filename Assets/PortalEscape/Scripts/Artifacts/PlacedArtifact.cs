using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PortalEscape
{
    public sealed class PlacedArtifact : MonoBehaviour
    {
        public string Id { get; private set; }
        public ArtifactDefinition Definition { get; private set; }
        public string TypeId => Definition.TypeId;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
        public Vector3 Scale => transform.localScale;
        public ARAnchor Anchor { get; private set; }
        public Vector3 MarkerPosition => transform.TransformPoint(markerOffset);

        private Vector3 markerOffset;

        internal void Initialize(ArtifactDefinition definition, string instanceId = null)
        {
            Id = instanceId ?? Guid.NewGuid().ToString("N");
            Definition = definition;
            var model = Instantiate(definition.Prefab, transform, false);
            model.transform.localPosition = Vector3.zero;

            var oldAnchor = model.GetComponent<AnchorOnSpawn>();
            if (oldAnchor != null)
                oldAnchor.enabled = false;
            foreach (var interactable in model.GetComponentsInChildren<XRBaseInteractable>(true))
                interactable.enabled = false;
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.useGravity = false;
                body.isKinematic = true;
            }

            if (definition.EnableRootRenderer && model.TryGetComponent<Renderer>(out var rootRenderer))
                rootRenderer.enabled = true;

            bool hasBounds = false;
            Bounds bounds = default;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                    continue;

                if (hasBounds)
                    bounds.Encapsulate(renderer.bounds);
                else
                    bounds = renderer.bounds;
                hasBounds = true;
            }

            if (hasBounds)
            {
                model.transform.localPosition = Vector3.up * -bounds.min.y;
                markerOffset = new Vector3(bounds.center.x, bounds.size.y + 0.025f, bounds.center.z);
                if (model.GetComponentInChildren<Collider>(true) == null)
                {
                    var box = gameObject.AddComponent<BoxCollider>();
                    box.center = bounds.center + model.transform.localPosition;
                    box.size = bounds.size;
                }
            }

            name = definition.DisplayName;
        }

        internal void Attach(ARAnchor anchor, Quaternion surfaceRotation)
        {
            Anchor = anchor;
            transform.SetParent(anchor.transform, false);
            transform.SetPositionAndRotation(anchor.transform.position, surfaceRotation);
        }
    }
}
