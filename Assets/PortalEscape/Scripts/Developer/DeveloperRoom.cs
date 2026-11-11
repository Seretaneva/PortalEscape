using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PortalEscape
{
    public enum DeveloperMode { Place, Edit, Delete }

    public sealed class DeveloperRoom : MonoBehaviour
    {
        [SerializeField] private Camera arCamera;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private XRRayInteractor legacyRayInteractor;
        [SerializeField] private ArtifactDefinition[] inventory;

        private readonly Dictionary<string, PlacedArtifact> artifacts = new();
        private readonly WorldTouchInput input = new();
        private ArtifactSurface surface;
        private ArtifactDrag drag;
        private bool busy;

        public Camera ArCamera => arCamera;
        public IReadOnlyList<ArtifactDefinition> Inventory => inventory;
        public IReadOnlyCollection<PlacedArtifact> Artifacts => artifacts.Values;
        public DeveloperMode Mode { get; private set; }
        public ArtifactDefinition Choice { get; private set; }
        public PlacedArtifact Selection { get; private set; }
        public bool HoldingArtifact => drag != null;
        public bool Busy => busy;
        public string Status { get; private set; } = "Scaneaza camera si alege un artefact.";

        private void Awake()
        {
            surface = new ArtifactSurface(raycastManager, anchorManager);
            if (legacyRayInteractor != null)
                legacyRayInteractor.gameObject.SetActive(false);
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            input.Reset();
            CancelDrag();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                return;

            input.Reset();
            CancelDrag();
        }

        private void OnDestroy()
        {
            foreach (var artifact in artifacts.Values)
                if (artifact != null)
                    surface.RemoveAnchor(artifact.Anchor);
        }

        private void Update()
        {
            if (!input.TryRead(!busy, out var gesture) || busy)
                return;

            if (Mode == DeveloperMode.Edit)
                HandleEditGesture(gesture);
            else if (gesture.Phase == WorldTouchPhase.Tap)
                HandleTap(gesture.Point);
        }

        public void SetMode(DeveloperMode mode)
        {
            if (busy || Mode == mode)
                return;
            CancelDrag();
            Mode = mode;
            Selection = null;
            input.Reset();
            Status = mode == DeveloperMode.Place ? "Atinge o suprafata detectata."
                : mode == DeveloperMode.Edit ? "Atinge si trage un artefact." : "Atinge un artefact.";
        }

        public void Choose(ArtifactDefinition definition)
        {
            if (busy || Mode != DeveloperMode.Place)
                return;
            Choice = definition;
            input.Reset();
            Status = "Atinge o suprafata detectata.";
        }

        public void Rotate(float degrees)
        {
            if (!busy && Mode == DeveloperMode.Edit && Selection != null && !HoldingArtifact)
                Selection.transform.Rotate(Vector3.up, degrees, Space.Self);
        }

        public void DeleteSelection()
        {
            if (busy || Mode != DeveloperMode.Delete || Selection == null)
                return;

            var artifact = Selection;
            Selection = null;
            artifacts.Remove(artifact.Id);
            artifact.gameObject.SetActive(false);
            surface.RemoveAnchor(artifact.Anchor);
            Destroy(artifact.gameObject);
            input.Reset();
            Status = "Artefact sters.";
        }

        private void HandleTap(Vector2 point)
        {
            var hit = PickArtifact(point, out _);
            if (Mode == DeveloperMode.Place)
            {
                if (Choice == null)
                    Status = "Alege un artefact din inventar.";
                else if (hit == null)
                    PlaceArtifact(point, Choice);
                return;
            }

            Selection = hit;
            Status = hit != null ? "Artefact selectat." : "Atinge un artefact.";
        }

        private void HandleEditGesture(WorldTouch gesture)
        {
            switch (gesture.Phase)
            {
                case WorldTouchPhase.Began:
                    var hit = PickArtifact(gesture.Point, out var grabPoint);
                    if (hit != null)
                    {
                        Selection = hit;
                        drag = new ArtifactDrag(hit, grabPoint);
                        Status = "Trage pentru a muta.";
                    }
                    break;
                case WorldTouchPhase.Tap:
                    if (drag == null)
                        Selection = null;
                    drag = null;
                    Status = Selection == null ? "Atinge si trage un artefact." : "Trage pentru a muta.";
                    break;
                case WorldTouchPhase.Drag:
                    if (!gesture.OverUi)
                        drag?.Update(gesture.Point, arCamera, surface, Time.unscaledDeltaTime);
                    break;
                case WorldTouchPhase.Drop:
                    if (drag == null)
                        break;
                    if (!gesture.OverUi)
                        drag.Update(gesture.Point, arCamera, surface, Time.unscaledDeltaTime);
                    if (drag.HasDestination)
                        CommitDrag(drag);
                    else
                        CancelDrag();
                    break;
                case WorldTouchPhase.Canceled:
                    CancelDrag();
                    break;
            }
        }

        private void CancelDrag()
        {
            drag?.Restore();
            drag = null;
        }

        private PlacedArtifact PickArtifact(Vector2 point, out Vector3 grabPoint)
        {
            grabPoint = default;
            PlacedArtifact closest = null;
            float distance = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(arCamera.ScreenPointToRay(point), float.PositiveInfinity,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var artifact = hit.collider.GetComponentInParent<PlacedArtifact>();
                if (artifact == null || !artifacts.ContainsKey(artifact.Id) || hit.distance >= distance)
                    continue;
                closest = artifact;
                distance = hit.distance;
                grabPoint = hit.point;
            }

            return closest;
        }

        private async void CommitDrag(ArtifactDrag dropped)
        {
            busy = true;
            Status = "Se fixeaza artefactul...";
            ARAnchor anchor = null;
            try
            {
                anchor = await surface.CreateAnchor(dropped.Destination);
                if (this == null || !isActiveAndEnabled || drag != dropped)
                    return;
                if (anchor == null || dropped.Artifact == null)
                {
                    Status = "Mutarea nu a putut fi fixata.";
                    return;
                }

                var oldAnchor = dropped.Artifact.Anchor;
                dropped.Artifact.Attach(anchor, dropped.Destination.rotation);
                drag = null;
                anchor = null;
                surface.RemoveAnchor(oldAnchor);
                Status = "Artefact mutat.";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null)
                    Status = "Mutarea nu a putut fi fixata.";
            }
            finally
            {
                if (drag == dropped)
                    CancelDrag();
                surface.RemoveAnchor(anchor);
                busy = false;
            }
        }

        private async void PlaceArtifact(Vector2 point, ArtifactDefinition definition)
        {
            if (!surface.TryGetPose(point, out var pose))
            {
                Status = "Scaneaza camera si incearca pe o suprafata detectata.";
                return;
            }

            busy = true;
            Status = "Se creeaza ancora...";
            ARAnchor anchor = null;
            GameObject root = null;
            try
            {
                anchor = await surface.CreateAnchor(pose);
                if (this == null || !isActiveAndEnabled)
                    return;
                if (anchor == null)
                {
                    Status = "Ancora nu a putut fi creata. Incearca din nou.";
                    return;
                }

                root = new GameObject("Artifact");
                root.SetActive(false);
                var artifact = root.AddComponent<PlacedArtifact>();
                artifact.Initialize(definition);
                artifact.Attach(anchor, pose.rotation);
                artifacts.Add(artifact.Id, artifact);
                root.SetActive(true);
                anchor = null;
                root = null;
                Status = "Artefact plasat.";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this != null)
                    Status = "Operatia a esuat. Incearca din nou.";
            }
            finally
            {
                if (root != null)
                    Destroy(root);
                surface.RemoveAnchor(anchor);
                busy = false;
            }
        }
    }
}
