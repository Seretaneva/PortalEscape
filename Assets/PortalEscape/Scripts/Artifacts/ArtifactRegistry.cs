using System;
using System.Collections.Generic;

namespace PortalEscape
{
    internal sealed class ArtifactRegistry
    {
        private readonly Dictionary<string, ArtifactDefinition> definitions = new(StringComparer.Ordinal);

        internal ArtifactRegistry(IEnumerable<ArtifactDefinition> inventory)
        {
            foreach (var definition in inventory)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.TypeId) || definition.Prefab == null)
                    throw new InvalidOperationException("Inventarul conține o definiție incompletă.");
                if (!definitions.TryAdd(definition.TypeId, definition))
                    throw new InvalidOperationException($"ID duplicat în inventar: {definition.TypeId}.");
            }
        }

        internal ArtifactDefinition Resolve(string typeId)
        {
            if (!definitions.TryGetValue(typeId, out var definition))
                throw new InvalidOperationException($"Prefab necunoscut pentru ID-ul «{typeId}».");
            return definition;
        }
    }
}
