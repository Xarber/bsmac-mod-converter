// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using CustomNotes;
using CustomNotes.Managers;
using UnityEngine;

namespace Bsmac.Qosmetics;

internal static class PackagedNote
{
    [Serializable]
    sealed class Descriptor
    {
        public int schema = 0;
        public string name = null;
        public string author = null;
        public string description = null;
        public bool showArrows = false;
    }

    internal static GameObject TryLoad(AssetBundle bundle, string filename)
    {
        if (Path.GetFileNameWithoutExtension(filename).Length != 64)
            return null;
        string model = Path.GetFullPath(Path.Combine(NoteAssetLoader.NotesDirectory, filename));
        if (Path.GetDirectoryName(model) != Path.Combine(NoteAssetLoader.NotesDirectory, "Qosmetics"))
            return null;
        var meta = JsonUtility.FromJson<Descriptor>(File.ReadAllText(Path.ChangeExtension(model, ".qos.json")));
        if (meta == null || meta.schema != 1)
            throw new InvalidDataException("Invalid Qosmetics descriptor");
        var source = bundle.LoadAsset<GameObject>("assets/qosmetics/_cyoob.prefab");
        if (source == null)
            throw new InvalidDataException("Qosmetics note prefab missing");
        var root = new GameObject("_CustomNote");
        root.SetActive(false);
        try
        {
            UnityEngine.Object.DontDestroyOnLoad(root);
            PlasmaColorPatch.Install();
            Copy("Notes/LeftArrow", "NoteLeft", required: true);
            Copy("Notes/RightArrow", "NoteRight", required: true);
            Copy("Notes/LeftDot", "NoteDotLeft");
            Copy("Notes/RightDot", "NoteDotRight");
            Copy("Chains/LeftHead", "BurstSliderHeadLeft");
            Copy("Chains/RightHead", "BurstSliderHeadRight");
            Copy("Chains/LeftLink", "BurstSliderLeft");
            Copy("Chains/RightLink", "BurstSliderRight");
            Copy("Notes/LeftDot", "BurstSliderHeadDotLeft");
            Copy("Notes/RightDot", "BurstSliderHeadDotRight");
            Copy("Bomb", "NoteBomb");
            var descriptor = root.AddComponent<NoteDescriptor>();
            descriptor.NoteName = meta.name;
            descriptor.AuthorName = meta.author;
            descriptor.Description = meta.description;
            descriptor.DisableBaseNoteArrows = !meta.showArrows;
            descriptor.UsesNoteColor = true;
            descriptor.NoteColorStrength = 1f;
            Debug.Log("[QosmeticsBridge] Adapted prepared package: " + meta.name);
            return root;
        }
        catch
        {
            UnityEngine.Object.Destroy(root);
            throw;
        }

        void Copy(string from, string to, bool required = false)
        {
            var original = source.transform.Find(from);
            if (original == null)
            {
                if (required) throw new InvalidDataException("Missing Qosmetics part: " + from);
                return;
            }
            var part = new GameObject(to);
            part.transform.SetParent(root.transform, false);
            // Bombs have no left/right color. Their authored material is retained.
            if (from != "Bomb") part.AddComponent<PlasmaColorTarget>();
            var clone = UnityEngine.Object.Instantiate(original.gameObject, part.transform, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            clone.SetActive(true);
            if (required && clone.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidDataException("Qosmetics part has no renderers: " + from);
        }
    }
}
