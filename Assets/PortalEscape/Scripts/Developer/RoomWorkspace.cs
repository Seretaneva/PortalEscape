using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace PortalEscape
{
    internal sealed class RoomWorkspace
    {
        private readonly DeveloperRoom developer;
        private readonly RoomStore store;
        private readonly Dictionary<string, Pose> sessionOrigins = new();
        private int savedLayoutRevision;

        internal RoomData Active { get; private set; }
        internal bool Dirty => Active == null ? developer.Artifacts.Count != 0
            : developer.LayoutRevision != savedLayoutRevision;

        internal RoomWorkspace(DeveloperRoom developer)
        {
            this.developer = developer;
            store = new RoomStore(Path.Combine(Application.persistentDataPath, "rooms"));
        }

        internal List<RoomData> List(out int unreadable) => store.List(out unreadable);

        internal void Create(string name)
        {
            bool adoptCurrentLayout = Active == null && developer.Artifacts.Count != 0;
            var origin = adoptCurrentLayout ? CaptureOrigin() : default;
            var layout = adoptCurrentLayout ? CaptureLayout(origin) : Array.Empty<ArtifactData>();
            var created = store.Create(name, layout);
            if (adoptCurrentLayout)
                sessionOrigins.Add(created.id, origin);
            else
                developer.ReplaceLayout(layout, Pose.identity);

            SetActive(created);
        }

        internal void Save()
        {
            if (Active == null)
                throw new InvalidOperationException("Creează sau încarcă o cameră înainte de salvare.");

            bool hasArtifacts = developer.Artifacts.Count != 0;
            var origin = hasArtifacts ? GetOrigin(Active.id) : default;
            var layout = hasArtifacts ? CaptureLayout(origin) : Array.Empty<ArtifactData>();
            var saved = store.Update(Active, Active.name, layout);
            if (hasArtifacts)
                sessionOrigins[saved.id] = origin;
            SetActive(saved);
        }

        internal void Load(RoomData selected)
        {
            var loaded = store.Read(selected.id);
            var origin = loaded.artifacts.Length != 0 ? GetOrigin(loaded.id) : Pose.identity;
            developer.ReplaceLayout(loaded.artifacts, origin);
            if (loaded.artifacts.Length != 0)
                sessionOrigins[loaded.id] = origin;
            SetActive(loaded);
        }

        internal void Rename(RoomData selected, string name)
        {
            bool isActive = Active?.id == selected.id;
            var expected = isActive ? Active : selected;
            var renamed = store.Update(expected, name, expected.artifacts);
            if (isActive)
                Active = renamed;
        }

        internal void Delete(RoomData selected)
        {
            bool isActive = Active?.id == selected.id;
            store.Delete(isActive ? Active : selected);
            sessionOrigins.Remove(selected.id);
            if (isActive)
            {
                developer.ReplaceLayout(Array.Empty<ArtifactData>(), Pose.identity);
                SetActive(null);
            }
        }

        private void SetActive(RoomData room)
        {
            Active = room;
            savedLayoutRevision = developer.LayoutRevision;
        }

        private Pose GetOrigin(string id) =>
            sessionOrigins.TryGetValue(id, out var origin) ? origin : CaptureOrigin();

        private Pose CaptureOrigin()
        {
            if (ARSession.state != ARSessionState.SessionTracking)
                throw new InvalidOperationException("Scanează camera până când urmărirea AR este activă.");

            var camera = developer.ArCamera.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.Cross(camera.right, Vector3.up);

            return new Pose(camera.position, Quaternion.LookRotation(forward, Vector3.up));
        }

        private ArtifactData[] CaptureLayout(Pose origin)
        {
            var layout = new List<ArtifactData>(developer.Artifacts.Count);
            var inverse = Quaternion.Inverse(origin.rotation);
            foreach (var artifact in developer.Artifacts)
            {
                if (artifact == null)
                    continue;

                layout.Add(new ArtifactData
                {
                    id = artifact.Id,
                    typeId = artifact.TypeId,
                    position = inverse * (artifact.Position - origin.position),
                    rotation = (inverse * artifact.Rotation).normalized,
                    scale = artifact.Scale
                });
            }

            return layout.ToArray();
        }
    }
}
