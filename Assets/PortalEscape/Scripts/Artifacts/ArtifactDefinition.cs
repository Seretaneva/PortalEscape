using System;
using UnityEngine;

namespace PortalEscape
{
    [Serializable]
    public sealed class ArtifactDefinition
    {
        [SerializeField] private string typeId;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private bool enableRootRenderer;

        public string TypeId => typeId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public bool EnableRootRenderer => enableRootRenderer;
    }
}
