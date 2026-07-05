// -----------------------------------------------------------------------------
//  FinishSetup.cs  — DECRYPTED  (Editor utility)
//
//  Finishes everything the auto-wirer can't do from FBX alone:
//
//   DECRYPTED ▸ Finish Setup ▸ Run All Steps
//     1. Wire AudioManager clip library (maps key strings → WAV files)
//     2. Create / position PlayerAnchors for every room
//     3. Apply URP materials to exhibit meshes (Hologram + EnergyScanline)
//     4. Hybrid cleanup — disable fake decorative Hero objects per room
//     5. Wire SceneController room list (roomRoot + playerAnchor per room)
//     6. Mark scene dirty
//
//  Run AFTER "Wire All Exhibits" so the exhibits are already in the scene.
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Decrypted.Core;
using Decrypted.Managers;

namespace Decrypted.EditorTools
{
    public static class FinishSetup
    {
        private const string AUDIO_SFX_DIR = "Assets/_Project/Audio/SFX";
        private const string AUDIO_AMB_DIR = "Assets/_Project/Audio/Ambient";
        private const string SHADER_DIR    = "Assets/_Project/Art/Shaders";

        // Room name → MuseumState mapping (order matches the flow)
        private static readonly (string roomName, MuseumState state, string ambKey,
                                  Vector3 anchorLocalPos, float anchorYaw)[] Rooms =
        {
            ("Room_Splash",        MuseumState.Splash,        "amb_atrium",  new Vector3(0, 0,  0.5f), 0f),
            ("Room_Atrium",        MuseumState.Atrium,        "amb_atrium",  new Vector3(0, 0,  1.0f), 0f),
            ("Room_AncientRoom",   MuseumState.AncientRoom,   "amb_ancient", new Vector3(0, 0, -0.5f), 0f),
            ("Room_WWIIRoom",      MuseumState.WWIIRoom,      "amb_wwii",    new Vector3(0, 0, -0.5f), 0f),
            ("Room_VaultRoom",     MuseumState.VaultRoom,     "amb_vault",   new Vector3(0, 0, -0.5f), 0f),
            ("Room_RevealChamber", MuseumState.RevealChamber, "amb_atrium",  new Vector3(0, 0, -0.5f), 0f),
        };

        // ---------------------------------------------------------------- menus

        [MenuItem("DECRYPTED/Finish Setup/Run All Steps %#f")]
        public static void RunAll()
        {
            WireAudioManager();
            CreatePlayerAnchors();
            ApplyShaderMaterials();
            HybridCleanup();
            WireSceneController();

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[FinishSetup] All steps done. Press Ctrl+S to save.");
        }

        // ---------------------------------------------------------------- steps

        [MenuItem("DECRYPTED/Finish Setup/1 - Wire AudioManager Clips")]
        public static void WireAudioManager()
        {
            var am = FindFirst<AudioManager>();
            if (am == null) { Warn("AudioManager not found in scene."); return; }

            var so = new SerializedObject(am);
            var lib = so.FindProperty("_library");
            if (lib == null) { Warn("AudioManager._library field not found."); return; }

            var clips = new List<(string key, string path)>
            {
                ("sfx_brass_click",   $"{AUDIO_SFX_DIR}/sfx_brass_click.wav"),
                ("sfx_gear_step",     $"{AUDIO_SFX_DIR}/sfx_gear_step.wav"),
                ("sfx_key_clack",     $"{AUDIO_SFX_DIR}/sfx_key_clack.wav"),
                ("sfx_lamp_on",       $"{AUDIO_SFX_DIR}/sfx_lamp_on.wav"),
                ("sfx_lever_pull",    $"{AUDIO_SFX_DIR}/sfx_lever_pull.wav"),
                ("sfx_success_chime", $"{AUDIO_SFX_DIR}/sfx_success_chime.wav"),
                ("sfx_vault_rumble",  $"{AUDIO_SFX_DIR}/sfx_vault_rumble.wav"),
                ("sfx_final_chord",   $"{AUDIO_SFX_DIR}/sfx_final_chord.wav"),
                ("amb_ancient",       $"{AUDIO_AMB_DIR}/amb_ancient.wav"),
                ("amb_wwii",          $"{AUDIO_AMB_DIR}/amb_wwii.wav"),
                ("amb_vault",         $"{AUDIO_AMB_DIR}/amb_vault.wav"),
                ("amb_atrium",        $"{AUDIO_AMB_DIR}/amb_atrium.wav"),
            };

            lib.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
            {
                var elem  = lib.GetArrayElementAtIndex(i);
                var clip  = AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i].path);
                if (clip == null) Warn($"Audio clip not found: {clips[i].path}");

                elem.FindPropertyRelative("key").stringValue = clips[i].key;
                elem.FindPropertyRelative("clip").objectReferenceValue = clip;
            }

            so.ApplyModifiedProperties();
            Debug.Log("[FinishSetup] AudioManager clip library wired (12 clips).");
        }

        [MenuItem("DECRYPTED/Finish Setup/2 - Create Player Anchors")]
        public static void CreatePlayerAnchors()
        {
            foreach (var (roomName, _, _, localPos, yaw) in Rooms)
            {
                var roomGo = GameObject.Find(roomName);
                if (roomGo == null) { Warn($"{roomName} not found — skipping."); continue; }

                // Reuse existing or create new PlayerAnchor.
                var existing = roomGo.transform.Find("PlayerAnchor");
                Transform anchor;
                if (existing != null)
                {
                    anchor = existing;
                }
                else
                {
                    var anchorGo = new GameObject("PlayerAnchor");
                    Undo.RegisterCreatedObjectUndo(anchorGo, "Create PlayerAnchor");
                    anchorGo.transform.SetParent(roomGo.transform, false);
                    anchor = anchorGo.transform;
                }

                anchor.localPosition = localPos;
                anchor.localRotation = Quaternion.Euler(0f, yaw, 0f);
                anchor.localScale    = Vector3.one;
            }
            Debug.Log("[FinishSetup] PlayerAnchors created/updated for all 6 rooms.");
        }

        [MenuItem("DECRYPTED/Finish Setup/3 - Apply Shader Materials")]
        public static void ApplyShaderMaterials()
        {
            var hologramShader    = Shader.Find("DECRYPTED/Hologram_URP");
            var energyShader      = Shader.Find("DECRYPTED/EnergyScanline_URP");
            var litShader         = Shader.Find("Universal Render Pipeline/Lit");

            if (hologramShader == null)
            { Warn("Hologram_URP shader not found — reimport Assets/_Project/Art/Shaders/"); return; }
            if (energyShader == null)
            { Warn("EnergyScanline_URP shader not found — reimport Assets/_Project/Art/Shaders/"); return; }

            // Reveal sculpture stages → Hologram_URP
            ApplyShaderToObjects(new[]
            {
                "Reveal_Stage_Roman", "Reveal_Stage_Gears", "Reveal_Stage_Circuit", "Reveal_MorphMesh"
            }, hologramShader, new Color(0.2f, 0.9f, 1.0f, 0.75f), emissionStrength: 1.2f);

            // Vault archive glow + display → EnergyScanline_URP
            ApplyShaderToObjects(new[]
            {
                "Vault_Archive_Glow", "Vault_Display"
            }, energyShader, new Color(0.2f, 0.9f, 1.0f, 1.0f), emissionStrength: 1.5f);

            Debug.Log("[FinishSetup] Shader materials applied.");
        }

        [MenuItem("DECRYPTED/Finish Setup/4 - Hybrid Cleanup (Disable Fake Heroes)")]
        public static void HybridCleanup()
        {
            int disabled = 0;
            // Tejas's system places a "Hero" child under MuseumDressing.
            // Keep the real interactive exhibit; disable the decorative stand-in.
            foreach (var (roomName, _, _, _, _) in Rooms)
            {
                var roomGo = GameObject.Find(roomName);
                if (roomGo == null) continue;

                var dressing = roomGo.transform.Find("MuseumDressing");
                if (dressing == null) continue;

                // Find any child named exactly "Hero" or starting with "HERO"
                foreach (Transform child in dressing)
                {
                    string n = child.name;
                    if (n == "Hero" || n.StartsWith("HERO", StringComparison.OrdinalIgnoreCase))
                    {
                        if (child.gameObject.activeSelf)
                        {
                            Undo.RecordObject(child.gameObject, "Disable Hero");
                            child.gameObject.SetActive(false);
                            disabled++;
                        }
                    }
                }
            }
            Debug.Log($"[FinishSetup] Hybrid cleanup: {disabled} decorative Hero object(s) disabled.");
        }

        [MenuItem("DECRYPTED/Finish Setup/5 - Wire SceneController Rooms")]
        public static void WireSceneController()
        {
            var sc = FindFirst<SceneController>();
            if (sc == null) { Warn("SceneController not found in scene."); return; }

            var so   = new SerializedObject(sc);
            var list = so.FindProperty("_rooms");
            if (list == null) { Warn("SceneController._rooms field not found."); return; }

            list.arraySize = Rooms.Length;
            for (int i = 0; i < Rooms.Length; i++)
            {
                var (roomName, state, ambKey, _, _) = Rooms[i];
                var roomGo  = GameObject.Find(roomName);
                var elem    = list.GetArrayElementAtIndex(i);

                elem.FindPropertyRelative("state").enumValueIndex = (int)state;

                if (roomGo == null) { Warn($"{roomName} not found — skipping room entry."); continue; }

                elem.FindPropertyRelative("roomRoot").objectReferenceValue = roomGo;
                elem.FindPropertyRelative("ambientKey").stringValue = ambKey;

                var anchor = roomGo.transform.Find("PlayerAnchor");
                if (anchor != null)
                    elem.FindPropertyRelative("playerAnchor").objectReferenceValue = anchor;
                else
                    Warn($"No PlayerAnchor found under {roomName} — run step 2 first.");
            }

            so.ApplyModifiedProperties();
            Debug.Log("[FinishSetup] SceneController room list wired for all 6 rooms.");
        }

        // ============================================================= helpers

        private static void ApplyShaderToObjects(string[] objNames, Shader shader,
                                                  Color baseColor, float emissionStrength)
        {
            foreach (var name in objNames)
            {
                // Search entire scene hierarchy.
                var go = FindGameObjectDeep(name);
                if (go == null) { Warn($"Object '{name}' not found in scene."); continue; }

                var renderers = go.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) renderers = new[] { go.GetComponent<Renderer>() };

                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var mat = new Material(shader);
                    mat.name = $"M_{name}_{shader.name.Replace("DECRYPTED/", "")}";
                    mat.SetColor("_BaseColor",       baseColor);
                    mat.SetColor("_EmissionColor",   baseColor);
                    mat.SetFloat("_EmissionStrength", emissionStrength);

                    // Save material asset so it survives domain reloads.
                    string dir  = "Assets/_Project/Art/Materials/AutoGenerated";
                    Directory.CreateDirectory(Path.Combine(
                        Application.dataPath, "../", dir).Replace("\\", "/").TrimStart('/'));
                    string path = $"{dir}/{mat.name}.mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(mat, path);
                        AssetDatabase.SaveAssets();
                        existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    }

                    Undo.RecordObject(r, "Apply Material");
                    r.sharedMaterial = existing ?? mat;
                }
            }
        }

        private static T FindFirst<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectOfType<T>();
        }

        private static GameObject FindGameObjectDeep(string name)
        {
            // GameObject.Find only finds active objects; search manually for inactive too.
            string lower = name.ToLowerInvariant();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = SearchDeep(root.transform, lower);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform SearchDeep(Transform t, string lowerName)
        {
            if (t.name.ToLowerInvariant() == lowerName) return t;
            foreach (Transform child in t)
            {
                var found = SearchDeep(child, lowerName);
                if (found != null) return found;
            }
            return null;
        }

        private static void Warn(string msg) =>
            Debug.LogWarning($"[FinishSetup] {msg}");
    }
}
