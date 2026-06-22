// ModelImportSetup.cs
// DECRYPTED — Editor utility
// After downloading a model from Sketchfab (or any source), drag it under the
// correct room root in the Hierarchy, select the ROOT GameObject of that model,
// then run the matching menu item below. The wizard will:
//   1. Rename children to the names the Unity scripts expect
//   2. Add the required component(s)
//   3. Wire internal references where possible
//
// Supported exhibits:
//   DECRYPTED ▸ Import Setup ▸ Caesar Disk  (expects InnerDisk child)
//   DECRYPTED ▸ Import Setup ▸ Enigma       (expects Rotor_0/1/2, Lever children)
//   DECRYPTED ▸ Import Setup ▸ Vault        (expects Vault_Door child)
//   DECRYPTED ▸ Import Setup ▸ Reveal       (auto-adds FinalRevealController)

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Decrypted.Editor
{
    public static class ModelImportSetup
    {
        // ------------------------------------------------------------------ Caesar
        [MenuItem("DECRYPTED/Import Setup/Caesar Disk")]
        static void SetupCaesarDisk()
        {
            var root = GetSelectedRoot("Caesar Disk setup");
            if (root == null) return;

            Log($"Setting up Caesar Disk on '{root.name}'");

            // Find InnerDisk — accept common alternative names from downloaded models
            var inner = FindChildByAny(root, "InnerDisk", "inner_disk", "Inner", "Disk_Inner",
                                       "rotating_disk", "RotatingDisk", "inner", "disk_b");
            if (inner == null)
            {
                LogWarn("No InnerDisk child found. Rename the rotating inner ring to 'InnerDisk' and re-run.");
            }
            else
            {
                inner.name = "InnerDisk";
                Log($"  InnerDisk → '{inner.name}'");
                EnsureComponent<Decrypted.Interaction.XRGrabTwistDisk>(inner);
            }

            var ctrl = EnsureComponent<Decrypted.Interaction.CaesarCipherController>(root);
            if (inner != null)
                SetPrivateField(ctrl, "_disk", inner.GetComponent<Decrypted.Interaction.XRGrabTwistDisk>());

            Log("Caesar Disk setup complete. Re-wire _disk in Inspector if auto-assign failed.");
            Selection.activeGameObject = root;
        }

        // ------------------------------------------------------------------ Enigma
        [MenuItem("DECRYPTED/Import Setup/Enigma Machine")]
        static void SetupEnigma()
        {
            var root = GetSelectedRoot("Enigma setup");
            if (root == null) return;

            Log($"Setting up Enigma on '{root.name}'");

            // Rotors — look for groups of 3 sibling children that look like wheels
            string[][] rotorAliases =
            {
                new[] { "Rotor_0", "Rotor0", "rotor_0", "rotor0", "Wheel_0", "wheel_0", "RotorA" },
                new[] { "Rotor_1", "Rotor1", "rotor_1", "rotor1", "Wheel_1", "wheel_1", "RotorB" },
                new[] { "Rotor_2", "Rotor2", "rotor_2", "rotor2", "Wheel_2", "wheel_2", "RotorC" },
            };
            var rotors = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                rotors[i] = FindChildByAny(root, rotorAliases[i]);
                if (rotors[i] != null)
                {
                    rotors[i].name = $"Rotor_{i}";
                    Log($"  Rotor_{i} → '{rotors[i].name}'");
                    var rd = EnsureComponent<Decrypted.Interaction.XRGrabTwistDisk>(rotors[i]);
                    var er = EnsureComponent<Decrypted.Interaction.EnigmaRotor>(rotors[i]);
                    SetPrivateField(er, "_rotorIndex", i);
                }
                else
                    LogWarn($"  Rotor_{i} not found. Rename manually and re-run.");
            }

            // Lever
            var lever = FindChildByAny(root, "Lever", "lever", "Handle", "CommitLever", "confirm_lever");
            if (lever != null)
            {
                lever.name = "Lever";
                Log($"  Lever → '{lever.name}'");
                EnsureComponent<Decrypted.Interaction.EnigmaLeverPull>(lever);
            }
            else LogWarn("  Lever not found. Rename the commit lever child to 'Lever' and re-run.");

            EnsureComponent<Decrypted.Interaction.EnigmaMachine>(root);
            EnsureComponent<Decrypted.Interaction.EnigmaController>(root);
            Log("Enigma setup complete. Wire Rotors[] and Lever fields in Inspector.");
            Selection.activeGameObject = root;
        }

        // ------------------------------------------------------------------ Vault
        [MenuItem("DECRYPTED/Import Setup/Vault Door")]
        static void SetupVault()
        {
            var root = GetSelectedRoot("Vault setup");
            if (root == null) return;

            Log($"Setting up Vault on '{root.name}'");

            var door = FindChildByAny(root, "Vault_Door", "VaultDoor", "Door", "door",
                                      "vault_door", "main_door", "DoorPanel");
            if (door != null)
            {
                door.name = "Vault_Door";
                Log($"  Vault_Door → '{door.name}'");
            }
            else LogWarn("  Vault_Door not found. Rename the door leaf child to 'Vault_Door' and re-run.");

            var kp = EnsureComponent<Decrypted.Interaction.VaultKeypad>(root);
            SetPrivateField(kp, "_passphrase", "VICTORY");

            var vc = EnsureComponent<Decrypted.Interaction.VaultController>(root);
            if (door != null)
                SetPrivateField(vc, "_door", door.transform);

            Log("Vault setup complete. Wire Door transform in Inspector if auto-assign failed.");
            Selection.activeGameObject = root;
        }

        // ------------------------------------------------------------------ Reveal
        [MenuItem("DECRYPTED/Import Setup/Reveal Sculpture")]
        static void SetupReveal()
        {
            var root = GetSelectedRoot("Reveal Sculpture setup");
            if (root == null) return;

            Log($"Setting up Reveal Sculpture on '{root.name}'");
            EnsureComponent<Decrypted.Interaction.FinalRevealController>(root);
            Log("FinalRevealController added. No additional wiring needed — auto-starts on room entry.");
            Selection.activeGameObject = root;
        }

        // ================================================================= helpers

        static GameObject GetSelectedRoot(string label)
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog(label, "Select the root GameObject of the imported model first.", "OK");
                return null;
            }
            return go;
        }

        static GameObject FindChildByAny(GameObject root, params string[] names)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                foreach (var n in names)
                    if (t.name.Equals(n, System.StringComparison.OrdinalIgnoreCase))
                        return t.gameObject;
            return null;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        // Reflection helper — sets a private/serialized field by name without
        // requiring the field to be public. Falls back silently if not found so
        // the wizard still runs even when a class changes its field names.
        static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null || value == null) return;
            var type = target.GetType();
            while (type != null)
            {
                var fi = type.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                if (fi != null) { fi.SetValue(target, value); return; }
                type = type.BaseType;
            }
        }

        static void Log(string msg)     => Debug.Log($"[ModelImportSetup] {msg}");
        static void LogWarn(string msg) => Debug.LogWarning($"[ModelImportSetup] {msg}");
    }
}
