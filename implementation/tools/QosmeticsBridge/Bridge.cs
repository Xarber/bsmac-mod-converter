// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using CustomNotes;
using UnityEngine;

namespace Bsmac.Qosmetics;

public static class Bridge
{
	private static readonly Dictionary<AssetBundle, GameObject> Roots = new Dictionary<AssetBundle, GameObject>();

	public static GameObject Load(AssetBundle bundle, string asset, string filename)
	{
		if (!string.Equals(Path.GetFileName(filename), "Plasma Notes.bloq", StringComparison.OrdinalIgnoreCase))
		{
			var packaged = PackagedNote.TryLoad(bundle, filename);
			if (packaged != null)
			{
				Roots.Add(bundle, packaged);
				return packaged;
			}
			return bundle.LoadAsset<GameObject>(asset);
		}
		GameObject source = bundle.LoadAsset<GameObject>("assets/qosmetics/_cyoob.prefab");
		if (source == null)
		{
			throw new Exception("Plasma prefab not found");
		}
		PlasmaColorPatch.Install();

        GameObject root = new GameObject("_CustomNote");
		root.SetActive(value: false);
		try
		{
			UnityEngine.Object.DontDestroyOnLoad(root);
			Copy("Notes/LeftArrow", "NoteLeft");
			Copy("Notes/RightArrow", "NoteRight");
			Copy("Notes/LeftDot", "NoteDotLeft");
			Copy("Notes/RightDot", "NoteDotRight");
			Copy("Chains/LeftHead", "BurstSliderHeadLeft");
			Copy("Chains/RightHead", "BurstSliderHeadRight");
			Copy("Chains/LeftLink", "BurstSliderLeft");
			Copy("Chains/RightLink", "BurstSliderRight");
			Copy("Notes/LeftDot", "BurstSliderHeadDotLeft");
			Copy("Notes/RightDot", "BurstSliderHeadDotRight");
			Copy("Bomb", "NoteBomb");
			NoteDescriptor noteDescriptor = root.AddComponent<NoteDescriptor>();
			noteDescriptor.NoteName = "Plasma Notes";
			noteDescriptor.AuthorName = "wolfy";
			noteDescriptor.DisableBaseNoteArrows = true;
			noteDescriptor.UsesNoteColor = true;
			noteDescriptor.NoteColorStrength = 1f;
			Roots.Add(bundle, root);
			Debug.Log("[QosmeticsBridge] Plasma Notes adapted successfully");
			return root;
		}
		catch
		{
			UnityEngine.Object.Destroy(root);
			throw;
		}
		void Copy(string from, string to)
		{
			Transform transform = source.transform.Find(from);
			if (transform == null)
			{
				throw new Exception("Missing Plasma object: " + from);
			}
			GameObject gameObject = new GameObject(to);
                gameObject.AddComponent<PlasmaColorTarget>();
			gameObject.transform.SetParent(root.transform, worldPositionStays: false);
			GameObject gameObject2 = UnityEngine.Object.Instantiate(transform.gameObject, gameObject.transform, worldPositionStays: false);
			gameObject2.transform.localPosition = Vector3.zero;
			gameObject2.transform.localRotation = Quaternion.identity;
			gameObject2.transform.localScale = Vector3.one;
			gameObject2.SetActive(value: true);
			Debug.Log("[QosmeticsBridge] Normalized transform: " + from);
			int num = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true).Length;
			if (num == 0)
			{
				throw new Exception("No renderers in " + from);
			}
			Debug.Log("[QosmeticsBridge] " + from + " -> " + to + " (" + num + " renderers)");
		}
	}

	public static void Unload(AssetBundle bundle, bool unloadObjects)
	{
		if (Roots.TryGetValue(bundle, out var value))
		{
			UnityEngine.Object.Destroy(value);
			Roots.Remove(bundle);
		}
		bundle.Unload(unloadObjects);
	}
}
