using System;
using UnityEngine;

namespace PortalEscape
{
    [Serializable]
    internal sealed class RoomData
    {
        internal const int CurrentSchema = 1;
        internal const int MaxNameLength = 48;

        public int schemaVersion;
        public int revision;
        public string id;
        public string name;
        public string createdUtc;
        public string modifiedUtc;
        public RoomOriginData origin;
        public ArtifactData[] artifacts;
    }

    [Serializable]
    internal sealed class RoomOriginData
    {
        public const string TemporarySpace = "temporary-local";

        public string coordinateSpace;
        public string cloudAnchorId;
    }

    [Serializable]
    internal sealed class ArtifactData
    {
        public string id;
        public string typeId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
}
