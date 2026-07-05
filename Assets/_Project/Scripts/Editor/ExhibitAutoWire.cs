// -----------------------------------------------------------------------------
//  ExhibitAutoWire.cs  — DECRYPTED  (Editor utility)
//
//  One-click wiring for all four interactive exhibits.
//  Run from:  DECRYPTED ▸ Wire Exhibits ▸ Wire All Exhibits
//
//  What it does per exhibit:
//   1. Loads the FBX from Assets/_Project/Art/Generated/
//   2. Removes any old instance already tagged "[AutoWired]" under the room
//   3. Instantiates the FBX as a child of the room at the HERO_ANCHOR position
//   4. Adds all required MonoBehaviour + Collider components
//   5. Wires every serialised field via SerializedObject (triggers Undo / dirty)
//   6. Marks the scene dirty so Ctrl+S saves it
//
//  Prereqs: the scene must be open and each Room_XXX GameObject must exist.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Decrypted.Interaction;

namespace Decrypted.EditorTools
{
    public static class ExhibitAutoWire
    {
        private const string FBX_DIR = "Assets/_Project/Art/Generated";
        private const string TAG      = "[AutoWired]";

        // Hero anchor local position (set by MuseumBuilder) — exhibit centrepiece.
        private static readonly Vector3 HeroLocal = new Vector3(0f, 0f, 2.0f);

        // ----------------------------------------------------------------- menus

        [MenuItem("DECRYPTED/Wire Exhibits/Wire All Exhibits %#w")]
        public static void WireAll()
        {
            WireCipherDisk();
            WireEnigma();
            WireVault();
            WireReveal();
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[ExhibitAutoWire] All four exhibits wired. Press Ctrl+S to save.");
        }

        [MenuItem("DECRYPTED/Wire Exhibits/Wire CipherDisk")]
        public static void WireCipherDisk()
        {
            var room = FindRoom("Room_AncientRoom"); if (room == null) return;
            var root = PlaceExhibit(room, "CipherDisk", "CipherDisk_Root");
            if (root == null) return;

            // InnerDisk — the XR-grabbable rotating disk
            var innerDisk = FindDeep(root.transform, "InnerDisk");
            if (innerDisk == null) { Warn("InnerDisk not found in CipherDisk FBX"); return; }

            // Add XRGrabTwistDisk to InnerDisk
            EnsureCollider<CapsuleCollider>(innerDisk.gameObject, c =>
            {
                c.radius  = 0.42f;
                c.height  = 0.12f;
                c.direction = 1; // Y-axis
            });
            var disk = GetOrAdd<XRGrabTwistDisk>(innerDisk.gameObject);
            using (var so = new SerializedObjectScope(disk))
            {
                so.Set("_localAxis",  Vector3.up);
                so.Set("_detents",    26);
                so.Set("_invert",     false);
            }

            // Build outer/inner anchor + glyph arrays
            var outerAnchors = new Transform[26];
            var innerAnchors = new Transform[26];
            var outerGlyphs  = new Renderer[26];
            var innerGlyphs  = new Renderer[26];

            for (int i = 0; i < 26; i++)
            {
                char ch = (char)('A' + i);
                outerAnchors[i] = FindDeep(root.transform, $"OuterAnchor_{ch}");
                innerAnchors[i] = FindDeep(root.transform, $"InnerAnchor_{ch}");
                outerGlyphs[i]  = FindRenderer(root.transform, $"OuterGlyph_{ch}");
                innerGlyphs[i]  = FindRenderer(root.transform, $"InnerGlyph_{ch}");
            }

            var ctrl = GetOrAdd<CaesarCipherController>(root);
            using (var so = new SerializedObjectScope(ctrl))
            {
                so.Set("_disk",         disk);
                so.SetObjectArray("_outerAnchors", outerAnchors);
                so.SetObjectArray("_innerAnchors", innerAnchors);
                so.SetObjectArray("_outerGlyphs",  outerGlyphs);
                so.SetObjectArray("_innerGlyphs",  innerGlyphs);
            }

            Debug.Log("[ExhibitAutoWire] CipherDisk wired in Room_AncientRoom.");
        }

        [MenuItem("DECRYPTED/Wire Exhibits/Wire Enigma")]
        public static void WireEnigma()
        {
            var room = FindRoom("Room_WWIIRoom"); if (room == null) return;
            var root = PlaceExhibit(room, "Enigma", "Enigma_Root");
            if (root == null) return;

            // ---- EnigmaMachine (cipher core) --------------------------------
            var machine = GetOrAdd<EnigmaMachine>(root);
            using (var so = new SerializedObjectScope(machine))
            {
                so.Set("_solutionKey",   "MAC");
                so.Set("_plaintextWord", "VICTORY");
                so.Set("_cipherWord",    "ZLDFDQO");
            }

            // ---- Three rotors -----------------------------------------------
            var rotorComponents = new EnigmaRotor[3];
            for (int i = 0; i < 3; i++)
            {
                var rotorT = FindDeep(root.transform, $"Rotor_{i}");
                if (rotorT == null) { Warn($"Rotor_{i} not found"); continue; }

                EnsureCollider<CapsuleCollider>(rotorT.gameObject, c =>
                {
                    c.radius    = 0.10f;
                    c.height    = 0.12f;
                    c.direction = 1; // Y-axis (rotor spins about Y)
                });

                var twistDisk = GetOrAdd<XRGrabTwistDisk>(rotorT.gameObject);
                using (var so = new SerializedObjectScope(twistDisk))
                {
                    so.Set("_localAxis", Vector3.up);
                    so.Set("_detents",   26);
                    so.Set("_invert",    false);
                }

                int idx = i; // capture for closure
                var rotor = GetOrAdd<EnigmaRotor>(rotorT.gameObject);
                using (var so = new SerializedObjectScope(rotor))
                {
                    so.Set("_rotorIndex", idx);
                    so.Set("_disk",       twistDisk);
                }
                rotorComponents[i] = rotor;
            }

            // ---- Lever -------------------------------------------------------
            var leverT = FindDeep(root.transform, "Lever");
            EnigmaLeverPull lever = null;
            if (leverT != null)
            {
                EnsureCollider<BoxCollider>(leverT.gameObject, c =>
                    c.size = new Vector3(0.06f, 0.06f, 0.30f));
                lever = GetOrAdd<EnigmaLeverPull>(leverT.gameObject);
            }
            else Warn("Lever not found in Enigma FBX");

            // ---- Keyboard (PokeButton per KeyCap) ----------------------------
            var keyList = new List<PokeButton>();
            foreach (char ch in "QWERTZUIOASDFGHJKPYXCVBNML")
            {
                var capT = FindDeep(root.transform, $"KeyCap_{ch}");
                if (capT == null) continue;
                EnsureCollider<CapsuleCollider>(capT.gameObject, c =>
                {
                    c.radius    = 0.036f;
                    c.height    = 0.04f;
                    c.direction = 1;
                });
                var btn = GetOrAdd<PokeButton>(capT.gameObject);
                using (var so = new SerializedObjectScope(btn))
                    so.Set("_value", ch.ToString());
                keyList.Add(btn);
            }
            var keyboard = GetOrAdd<EnigmaKeyboard>(root);
            using (var so = new SerializedObjectScope(keyboard))
                so.SetObjectList("_keys", keyList);

            // ---- Lampboard ---------------------------------------------------
            var lampRenderers = new Renderer[26];
            for (int i = 0; i < 26; i++)
            {
                char ch = (char)('A' + i);
                lampRenderers[i] = FindRenderer(root.transform, $"Lamp_{ch}");
            }
            var lampboard = GetOrAdd<EnigmaLampboard>(root);
            using (var so = new SerializedObjectScope(lampboard))
                so.SetObjectArray("_lamps", lampRenderers);

            // ---- EnigmaController (wires everything together) ----------------
            var ctrl = GetOrAdd<EnigmaController>(root);
            using (var so = new SerializedObjectScope(ctrl))
            {
                so.Set("_machine",   machine);
                so.SetObjectArray("_rotors", rotorComponents);
                so.Set("_keyboard",  keyboard);
                so.Set("_lampboard", lampboard);
                so.Set("_lever",     lever);
            }

            Debug.Log("[ExhibitAutoWire] Enigma wired in Room_WWIIRoom.");
        }

        [MenuItem("DECRYPTED/Wire Exhibits/Wire Vault")]
        public static void WireVault()
        {
            var room = FindRoom("Room_VaultRoom"); if (room == null) return;
            var root = PlaceExhibit(room, "Vault", "Vault_Root");
            if (root == null) return;

            // Door — VaultController expects the door transform itself
            var doorT  = FindDeep(root.transform, "Vault_Door");
            var ringT  = FindDeep(root.transform, "Vault_LockingRing");

            var statusLights = new Renderer[3];
            for (int i = 0; i < 3; i++)
                statusLights[i] = FindRenderer(root.transform, $"Vault_StatusLight_{i}");

            var archiveGlow = FindRenderer(root.transform, "Vault_Archive_Glow");

            var vaultCtrl = GetOrAdd<VaultController>(root);
            using (var so = new SerializedObjectScope(vaultCtrl))
            {
                so.Set("_door",         doorT);
                so.Set("_lockingRing",  ringT);
                so.SetObjectArray("_statusLights",   statusLights);
                so.SetObjectArray("_archiveEmissive",
                    archiveGlow != null ? new Renderer[] { archiveGlow } : new Renderer[0]);
            }

            // Keypad buttons — VaultKeypadButton per key
            var letterKeys = new List<VaultKeypadButton>();
            foreach (char ch in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            {
                var keyT = FindDeep(root.transform, $"VaultKey_{ch}");
                if (keyT == null) continue;
                EnsureCollider<BoxCollider>(keyT.gameObject, c =>
                    c.size = new Vector3(0.05f, 0.05f, 0.04f));
                var btn = GetOrAdd<VaultKeypadButton>(keyT.gameObject);
                using (var so = new SerializedObjectScope(btn))
                {
                    so.Set("_value",        ch.ToString());
                    so.Set("_faceRenderer", keyT.GetComponent<Renderer>() ??
                                            keyT.GetComponentInChildren<Renderer>());
                }
                letterKeys.Add(btn);
            }

            VaultKeypadButton enterBtn = null, clearBtn = null;
            var enterT = FindDeep(root.transform, "VaultKey_ENTER");
            var clearT = FindDeep(root.transform, "VaultKey_CLEAR");
            if (enterT != null)
            {
                EnsureCollider<BoxCollider>(enterT.gameObject, c =>
                    c.size = new Vector3(0.07f, 0.05f, 0.04f));
                enterBtn = GetOrAdd<VaultKeypadButton>(enterT.gameObject);
                using (var so = new SerializedObjectScope(enterBtn))
                    so.Set("_value", "ENTER");
            }
            if (clearT != null)
            {
                EnsureCollider<BoxCollider>(clearT.gameObject, c =>
                    c.size = new Vector3(0.07f, 0.05f, 0.04f));
                clearBtn = GetOrAdd<VaultKeypadButton>(clearT.gameObject);
                using (var so = new SerializedObjectScope(clearBtn))
                    so.Set("_value", "CLEAR");
            }

            var display = FindDeep(root.transform, "Vault_Display");

            var keypad = GetOrAdd<VaultKeypad>(root);
            using (var so = new SerializedObjectScope(keypad))
            {
                so.SetObjectList("_letterKeys", letterKeys);
                so.Set("_enterKey",   enterBtn);
                so.Set("_clearKey",   clearBtn);
                so.Set("_vault",      vaultCtrl);
                // _display is a TMP_Text; skip if not present — user can assign later
            }

            Debug.Log("[ExhibitAutoWire] Vault wired in Room_VaultRoom.");
        }

        [MenuItem("DECRYPTED/Wire Exhibits/Wire Reveal")]
        public static void WireReveal()
        {
            var room = FindRoom("Room_RevealChamber"); if (room == null) return;
            var root = PlaceExhibit(room, "RevealSculpture", "Reveal_Root");
            if (root == null) return;

            var stages = new Renderer[3];
            stages[0] = FindRenderer(root.transform, "Reveal_Stage_Roman");
            stages[1] = FindRenderer(root.transform, "Reveal_Stage_Gears");
            stages[2] = FindRenderer(root.transform, "Reveal_Stage_Circuit");

            var pivotT = FindDeep(root.transform, "Reveal_Pivot");

            var ctrl = GetOrAdd<FinalRevealController>(root);
            using (var so = new SerializedObjectScope(ctrl))
            {
                so.SetObjectArray("_stages", stages);
                if (pivotT != null) so.Set("_spinRoot", pivotT);
            }

            Debug.Log("[ExhibitAutoWire] RevealSculpture wired in Room_RevealChamber.");
        }

        // ============================================================= helpers

        private static GameObject FindRoom(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) Warn($"Room '{name}' not found in scene — open the main scene first.");
            return go;
        }

        private static GameObject PlaceExhibit(GameObject room, string fbxName, string rootChildName)
        {
            // Remove any previously auto-wired instance.
            var existing = room.transform.Find(TAG + fbxName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log($"[ExhibitAutoWire] Removed old {fbxName} instance.");
            }

            // Load FBX prefab.
            string path = $"{FBX_DIR}/{fbxName}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Warn($"FBX not found at '{path}'. Pull main first."); return null; }

            // Instantiate under room.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room.transform);
            instance.name = TAG + fbxName;
            Undo.RegisterCreatedObjectUndo(instance, $"Place {fbxName}");

            // Position at the hero anchor.
            var anchor = FindDeep(room.transform, "HERO_ANCHOR_DropInteractiveExhibitHere");
            instance.transform.localPosition = anchor != null ? anchor.localPosition : HeroLocal;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale    = Vector3.one;

            // Find the FBX root child (the named ROOT empty from gen_common.parent_all_to_root).
            var rootT = instance.transform.Find(rootChildName) ?? instance.transform;
            return rootT.gameObject;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            string lower = name.ToLowerInvariant();
            return FindDeepRecurse(parent, lower);
        }

        private static Transform FindDeepRecurse(Transform t, string lowerName)
        {
            foreach (Transform child in t)
            {
                if (child.name.ToLowerInvariant() == lowerName) return child;
                var found = FindDeepRecurse(child, lowerName);
                if (found != null) return found;
            }
            return null;
        }

        private static Renderer FindRenderer(Transform root, string name)
        {
            var t = FindDeep(root, name);
            if (t == null) return null;
            return t.GetComponent<Renderer>() ?? t.GetComponentInChildren<Renderer>();
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }

        private static void EnsureCollider<T>(GameObject go, Action<T> configure)
            where T : Collider
        {
            if (go.GetComponent<Collider>() != null) return;
            var col = Undo.AddComponent<T>(go);
            configure(col);
        }

        private static void Warn(string msg) =>
            Debug.LogWarning($"[ExhibitAutoWire] {msg}");

        // ------------------------------------------ SerializedObject wrapper

        private sealed class SerializedObjectScope : IDisposable
        {
            private readonly SerializedObject _so;
            public SerializedObjectScope(UnityEngine.Object target)
                => _so = new SerializedObject(target);

            public void Set(string field, UnityEngine.Object value)
            {
                var p = _so.FindProperty(field);
                if (p != null) { p.objectReferenceValue = value; }
                else Debug.LogWarning($"[AutoWire] Field '{field}' not found");
            }
            public void Set(string field, int value)
            {
                var p = _so.FindProperty(field);
                if (p != null) p.intValue = value;
            }
            public void Set(string field, float value)
            {
                var p = _so.FindProperty(field);
                if (p != null) p.floatValue = value;
            }
            public void Set(string field, bool value)
            {
                var p = _so.FindProperty(field);
                if (p != null) p.boolValue = value;
            }
            public void Set(string field, string value)
            {
                var p = _so.FindProperty(field);
                if (p != null) p.stringValue = value;
            }
            public void Set(string field, Vector3 value)
            {
                var p = _so.FindProperty(field);
                if (p != null) p.vector3Value = value;
            }

            public void SetObjectArray<T>(string field, T[] values) where T : UnityEngine.Object
            {
                var p = _so.FindProperty(field);
                if (p == null) { Debug.LogWarning($"[AutoWire] Array field '{field}' not found"); return; }
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            public void SetObjectList<T>(string field, List<T> values) where T : UnityEngine.Object
            {
                var p = _so.FindProperty(field);
                if (p == null) { Debug.LogWarning($"[AutoWire] List field '{field}' not found"); return; }
                p.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            public void Dispose() => _so.ApplyModifiedProperties();
        }
    }
}
