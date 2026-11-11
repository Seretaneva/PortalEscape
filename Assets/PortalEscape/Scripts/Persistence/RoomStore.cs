using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PortalEscape
{
    internal sealed class RoomStore
    {
        private readonly string directory;

        internal RoomStore(string directory) => this.directory = directory;

        internal List<RoomData> List(out int unreadable)
        {
            unreadable = 0;
            var rooms = new List<RoomData>();
            Directory.CreateDirectory(directory);
            foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
            {
                try
                {
                    rooms.Add(Read(Path.GetFileNameWithoutExtension(path)));
                }
                catch (Exception exception) when (exception is IOException ||
                    exception is UnauthorizedAccessException || exception is FormatException)
                {
                    unreadable++;
                    Debug.LogWarning($"Portal Escape: {Path.GetFileName(path)}: {exception.Message}");
                }
            }

            rooms.Sort((left, right) =>
            {
                int recent = string.CompareOrdinal(right.modifiedUtc, left.modifiedUtc);
                return recent == 0 ? string.CompareOrdinal(left.id, right.id) : recent;
            });
            return rooms;
        }

        internal RoomData Read(string id)
        {
            var json = File.ReadAllText(RoomPath(id));
            RoomData room;
            try
            {
                room = JsonUtility.FromJson<RoomData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("JSON invalid.", exception);
            }

            Validate(room, id);
            return room;
        }

        internal RoomData Create(string name, ArtifactData[] artifacts)
        {
            string now = UtcNow();
            var room = new RoomData
            {
                schemaVersion = RoomData.CurrentSchema,
                revision = 1,
                id = Guid.NewGuid().ToString("N"),
                name = NormalizeName(name),
                createdUtc = now,
                modifiedUtc = now,
                origin = new RoomOriginData { coordinateSpace = RoomOriginData.TemporarySpace, cloudAnchorId = "" },
                artifacts = artifacts
            };
            Write(room, false);
            return room;
        }

        internal RoomData Update(RoomData expected, string name, ArtifactData[] artifacts)
        {
            var current = Read(expected.id);
            CheckRevision(expected, current);
            current.name = NormalizeName(name);
            current.artifacts = artifacts;
            current.modifiedUtc = UtcNow();
            current.revision = checked(current.revision + 1);
            Write(current, true);
            return current;
        }

        internal void Delete(RoomData expected)
        {
            CheckRevision(expected, Read(expected.id));
            File.Delete(RoomPath(expected.id));
        }

        private void Write(RoomData room, bool replace)
        {
            Validate(room, room.id);
            Directory.CreateDirectory(directory);
            string destination = RoomPath(room.id);
            // Keep the temporary file on the same volume for an atomic move or replacement.
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(room, true));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                if (replace)
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private string RoomPath(string id)
        {
            if (!Guid.TryParseExact(id, "N", out var guid) || guid.ToString("N") != id)
                throw new FormatException("ID de cameră invalid.");

            return Path.Combine(directory, id + ".json");
        }

        private static void CheckRevision(RoomData expected, RoomData current)
        {
            if (current.revision != expected.revision)
                throw new IOException("Camera s-a modificat pe disc. Reîncarcă datele înainte de a o modifica.");
        }

        private static string NormalizeName(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > RoomData.MaxNameLength)
                throw new FormatException($"Introdu un nume de 1–{RoomData.MaxNameLength} de caractere.");
            return name;
        }

        private static string UtcNow() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        private static bool IsUtcDate(string value) =>
            DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var date) && date.Kind == DateTimeKind.Utc;

        private static void Validate(RoomData room, string expectedId)
        {
            if (room == null || room.schemaVersion != RoomData.CurrentSchema)
                throw new FormatException("Versiune JSON absentă sau incompatibilă.");
            if (room.id != expectedId || room.revision < 1 || !IsUtcDate(room.createdUtc) ||
                !IsUtcDate(room.modifiedUtc) || room.origin == null ||
                room.origin.coordinateSpace != RoomOriginData.TemporarySpace || room.artifacts == null)
                throw new FormatException("Date de cameră incomplete sau invalide.");

            NormalizeName(room.name);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var artifact in room.artifacts)
            {
                if (artifact == null || !Guid.TryParseExact(artifact.id, "N", out _) || !ids.Add(artifact.id) ||
                    string.IsNullOrWhiteSpace(artifact.typeId) || !IsFinite(artifact.position) ||
                    !IsFinite(artifact.scale) || artifact.scale.x <= 0 || artifact.scale.y <= 0 ||
                    artifact.scale.z <= 0 || !IsRotation(artifact.rotation))
                    throw new FormatException("Date de artefact invalide sau ID duplicat.");
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsRotation(Quaternion value)
        {
            float lengthSquared = Quaternion.Dot(value, value);
            return float.IsFinite(lengthSquared) && Math.Abs(lengthSquared - 1f) <= 0.01f;
        }
    }
}
