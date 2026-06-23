// -----------------------------------------------------------------------------
//  MuseumBuilder.cs   (Editor)
//  DECRYPTED, A Walk Through the History of Secret Writing
//
//  The orchestrator that turns the bare room roots into a dense, grand,
//  world-class museum. It dresses every gallery authored in MuseumContent
//  (Entrance, Atrium, Ancient, WWII, Vault, Reveal and the Complete hall). For
//  each one it:
//
//    1. Builds a GRAND architectural shell, large footprint, tall ceilings,
//       coffered ceiling + laylight, colonnade, framed archway doorways, baseboard
//       and cornice, a runner-and-border floor, wall sconces.
//    2. Drops a dramatic procedural HERO centerpiece (entrance portal, armillary
//       globe, cipher disk, Enigma, vault door, synthesis sculpture, or the
//       closing-plaque panel) on a plinth, with rope barrier, benches and real
//       spotlights, except the entrance (own layout) and the baked-only Complete hall.
//    3. Densely DRESSES the perimeter from MuseumContent, display cases,
//       pedestals, tablet cases, wall reliefs, framed infographics, a portrait
//       gallery of pioneers, a wall timeline, kiosks, every one carrying unique,
//       authored museum copy.
//    4. Adds the small details that sell realism, signage, hanging gallery signs,
//       a directory, banners, planters, a trash bin, security cameras, an exit sign.
//
//  Everything generated lives under a single "MuseumDressing" child per room, so a
//  rebuild is idempotent and a one-click "Clear" cleanly reverses it. Nothing here
//  touches the existing managers, XR rig or save data, it is purely additive set
//  dressing parented beneath each Room_* root, in that room's local space.
//
//  Menu:  DECRYPTED ▸ Museum ▸ Build Full Museum   (Ctrl/Cmd+Shift+M)
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Decrypted.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Decrypted.EditorTools
{
    public static class MuseumBuilder
    {
        private const string DressingName = "MuseumDressing";

        // Per-gallery look: material keys for floor, wall, ceiling, trim, accent
        // metal, sconce glow and floor runner; the hero-light colour (Key) and
        // Intensity; and the footprint W, D and ceiling height H.
        private struct Style
        {
            public string Floor, Wall, Ceil, Trim, Accent, Sconce, Runner;
            public Color Key; public float Intensity; public float W, D, H;
        }

        private static Style StyleFor(string key)
        {
            switch (key)
            {
                case "entrance": return new Style { Floor = "marbleDark", Wall = "marbleDark", Ceil = "ceiling", Trim = "brass", Accent = "brass", Sconce = "glowGold", Runner = "carpetRed", Key = new Color(1f, 0.86f, 0.6f), Intensity = 14f, W = 15f, D = 12f, H = 7.5f };
                case "atrium": return new Style { Floor = "marbleLight", Wall = "plasterWarm", Ceil = "marbleLight", Trim = "brassDark", Accent = "brass", Sconce = "glowWarm", Runner = "carpetRed", Key = new Color(1f, 0.92f, 0.78f), Intensity = 11f, W = 17f, D = 14f, H = 8.5f };
                case "ancient": return new Style { Floor = "sandstone", Wall = "plasterWarm", Ceil = "stone", Trim = "bronze", Accent = "bronze", Sconce = "glowAmber", Runner = "carpetRed", Key = new Color(1f, 0.74f, 0.42f), Intensity = 13f, W = 16f, D = 13f, H = 7.5f };
                case "wwii": return new Style { Floor = "concrete", Wall = "plasterCool", Ceil = "ceiling", Trim = "steel", Accent = "steel", Sconce = "glowWarm", Runner = "carpetBlue", Key = new Color(0.95f, 0.95f, 1f), Intensity = 12f, W = 16f, D = 13f, H = 7.5f };
                case "vault": return new Style { Floor = "marbleDark", Wall = "plasterCool", Ceil = "ceiling", Trim = "chrome", Accent = "steel", Sconce = "glowCool", Runner = "carpetBlue", Key = new Color(0.7f, 0.9f, 1f), Intensity = 12f, W = 16f, D = 13f, H = 7.5f };
                case "reveal": return new Style { Floor = "marbleDark", Wall = "marbleDark", Ceil = "ceiling", Trim = "gold", Accent = "gold", Sconce = "glowGold", Runner = "carpetRed", Key = new Color(1f, 0.88f, 0.7f), Intensity = 16f, W = 15f, D = 13f, H = 8f };
                // Completion hall: warmer than the puzzle rooms. Ceiling height is
                // intentionally half of the original 8.5 m (= 4.25 m) so the room is
                // not cavernous; floor footprint is unchanged so props are not crushed.
                case "complete": return new Style { Floor = "marbleLight", Wall = "plasterWarm", Ceil = "marbleLight", Trim = "gold", Accent = "brass", Sconce = "glowGold", Runner = "carpetRed", Key = new Color(1f, 0.9f, 0.72f), Intensity = 14f, W = 16f, D = 14f, H = 4.25f };
                default: return new Style { Floor = "concrete", Wall = "plasterNeutral", Ceil = "ceiling", Trim = "brass", Accent = "brass", Sconce = "glowWarm", Runner = "carpetRed", Key = Color.white, Intensity = 12f, W = 16f, D = 13f, H = 7.5f };
            }
        }

        // =====================================================================
        //  Menu
        // =====================================================================
        [MenuItem("DECRYPTED/Museum/Build Full Museum %#m", priority = 0)]
        public static void BuildAll()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Museum] Exit Play Mode first."); return; }
            MuseumKit.Init();

            int built = 0, missing = 0;
            foreach (var g in MuseumContent.Galleries)
            {
                var room = FindRoom(g.State);
                if (room == null) { Debug.LogWarning($"[Museum] Room_{g.State} not found, skipped."); missing++; continue; }
                BuildGallery(g, room);
                built++;
            }

            AssetDatabase.SaveAssets();
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Museum] Built {built} galleries ({missing} rooms missing). " +
                      "Shared materials in " + MuseumKit.MatDir + ". " +
                      "Tip: 'Window ▸ Rendering ▸ Lighting ▸ Generate Lighting' to bake for final polish.");
        }

        [MenuItem("DECRYPTED/Museum/Clear Generated Dressing", priority = 1)]
        public static void ClearAll()
        {
            if (Application.isPlaying) return;
            int n = 0;
            foreach (var g in MuseumContent.Galleries)
            {
                var room = FindRoom(g.State);
                if (room == null) continue;
                var d = room.Find(DressingName);
                if (d != null) { Object.DestroyImmediate(d.gameObject); n++; }
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Museum] Cleared dressing from {n} galleries.");
        }

        [MenuItem("DECRYPTED/Museum/Rebuild One ▸ Selected Room", priority = 2)]
        public static void RebuildSelected()
        {
            if (Selection.activeGameObject == null) { Debug.LogWarning("[Museum] Select a Room_* object first."); return; }
            string n = Selection.activeGameObject.name;
            foreach (var g in MuseumContent.Galleries)
            {
                if ("Room_" + g.State == n)
                {
                    MuseumKit.Init();
                    BuildGallery(g, Selection.activeGameObject.transform);
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Debug.Log($"[Museum] Rebuilt {n}.");
                    return;
                }
            }
            Debug.LogWarning("[Museum] Selection is not a recognised Room_* root.");
        }

        /// <summary>Build/refresh a single gallery by state (used to stamp the
        /// completion hall without rebuilding the whole museum). Idempotent.</summary>
        public static void BuildOne(MuseumState state)
        {
            if (Application.isPlaying) { Debug.LogWarning("[Museum] Exit Play Mode first."); return; }
            MuseumKit.Init();
            if (!MuseumContent.TryGet(state, out var g)) { Debug.LogWarning($"[Museum] No gallery authored for {state}."); return; }
            var room = FindRoom(state);
            if (room == null) { Debug.LogWarning($"[Museum] Room_{state} not found."); return; }
            BuildGallery(g, room);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Museum] Built Room_{state}.");
        }

        // =====================================================================
        //  Gallery build
        // =====================================================================
        private static void BuildGallery(Gallery g, Transform room)
        {
            var old = room.Find(DressingName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(DressingName);
            Undo.RegisterCreatedObjectUndo(root, "Build Museum");
            root.transform.SetParent(room, false);

            var st = StyleFor(g.Key);
            BuildShell(root.transform, g, st);
            BuildHero(root.transform, g, st);
            DressPerimeter(root.transform, g, st);
            BuildSignageAndDecor(root.transform, g, st);
            BuildLighting(root.transform, g, st);
        }

        // ---------------------------------------------------------------- SHELL
        private static void BuildShell(Transform p, Gallery g, Style st)
        {
            var shell = MuseumKit.Group(p, "Shell");
            float W = st.W, D = st.D, H = st.H, t = 0.3f;
            float hw = W * 0.5f, hd = D * 0.5f;

            // Floor: slab + central runner + border inlay (sits just above the legacy plane)
            MuseumKit.Box(shell.transform, "FloorSlab", new Vector3(0, -0.1f + 0.006f, 0), new Vector3(W, 0.2f, D), st.Floor, default, true);
            MuseumKit.Box(shell.transform, "Runner", new Vector3(0, 0.012f, 0), new Vector3(2.4f, 0.01f, D - 0.4f), st.Runner);
            MuseumKit.Box(shell.transform, "BorderN", new Vector3(0, 0.012f, hd - 0.5f), new Vector3(W - 1f, 0.01f, 0.12f), st.Trim);
            MuseumKit.Box(shell.transform, "BorderS", new Vector3(0, 0.012f, -hd + 0.5f), new Vector3(W - 1f, 0.01f, 0.12f), st.Trim);
            FloorInlay(shell.transform, new Vector3(0, 0.014f, HeroZ), st);

            // Grand ceiling: slab + coffered field + glowing central laylight.
            MuseumKit.Box(shell.transform, "Ceiling", new Vector3(0, H + 0.1f, 0), new Vector3(W, 0.2f, D), st.Ceil);
            CofferedCeiling(shell.transform, W, D, H, st);
            CeilingLaylight(shell.transform, W, D, H, st);

            // Walls, back/front solid, side walls with framed doorways
            MuseumKit.Box(shell.transform, "WallBack", new Vector3(0, H * 0.5f, hd), new Vector3(W, H, t), st.Wall, default, true);
            MuseumKit.Box(shell.transform, "WallFront", new Vector3(0, H * 0.5f, -hd), new Vector3(W, H, t), st.Wall, default, true);
            bool entry = g.State != MuseumState.Splash;
            bool exit = g.State != MuseumState.RevealChamber;
            SideWall(shell.transform, "WallLeft", -hw, D, H, t, st, entry);
            SideWall(shell.transform, "WallRight", hw, D, H, t, st, exit);

            // Baseboard + multi-tier crown cornice on every wall.
            foreach (float z in new[] { hd - t * 0.5f, -hd + t * 0.5f })
            {
                MuseumKit.Box(shell.transform, "Base", new Vector3(0, 0.12f, z), new Vector3(W, 0.24f, 0.06f), st.Trim);
                CrownCornice(shell.transform, true, z, W, H, st);
            }
            foreach (float xw in new[] { hw - t * 0.5f, -hw + t * 0.5f })
                CrownCornice(shell.transform, false, xw, D, H, st);

            // Wainscot panelling on the long walls + clerestory daylight windows.
            WallDressing(shell.transform, W, D, H, st);
            Clerestory(shell.transform, W, D, H, st);

            // Colonnade + classical entablature (architrave / triglyph frieze / dentils).
            int cols = 4;
            for (int i = 0; i < cols; i++)
            {
                float cx = Mathf.Lerp(-hw + 2f, hw - 2f, i / (float)(cols - 1));
                MuseumProps.Column(shell.transform, new Vector3(cx, 0, hd - 0.9f), H - 0.5f, 0.3f, "marbleLight");
                MuseumProps.Column(shell.transform, new Vector3(cx, 0, -hd + 0.9f), H - 0.5f, 0.3f, "marbleLight");
            }
            Entablature(shell.transform, new Vector3(0, H - 0.55f, hd - 0.9f), W, 180f, st);
            Entablature(shell.transform, new Vector3(0, H - 0.55f, -hd + 0.9f), W, 0f, st);

            // Engaged pilasters flanking the long walls near the corners.
            foreach (float pz in new[] { hd - 0.18f, -hd + 0.18f })
                foreach (float px in new[] { -hw + 0.8f, hw - 0.8f })
                {
                    MuseumKit.Box(shell.transform, "Pilaster", new Vector3(px, (H - 0.5f) * 0.5f, pz), new Vector3(0.5f, H - 0.5f, 0.14f), "marbleLight");
                    MuseumKit.Box(shell.transform, "PilCap", new Vector3(px, H - 0.6f, pz), new Vector3(0.64f, 0.16f, 0.22f), st.Trim);
                }

            // Archways framing the doorways.
            if (entry) MuseumProps.Archway(shell.transform, new Vector3(-hw + 0.25f, 0, 0), 90f, 2.6f, 3.0f, "marbleLight");
            if (exit) MuseumProps.Archway(shell.transform, new Vector3(hw - 0.25f, 0, 0), 90f, 2.6f, 3.0f, "marbleLight");

            // Wall sconces (emissive, free light) on the long walls.
            for (int i = 0; i < 4; i++)
            {
                float sx = Mathf.Lerp(-hw + 1.6f, hw - 1.6f, i / 3f);
                Sconce(shell.transform, new Vector3(sx, H * 0.42f, hd - 0.16f), 180f, st.Sconce);
                Sconce(shell.transform, new Vector3(sx, H * 0.42f, -hd + 0.16f), 0f, st.Sconce);
            }

            // Chandeliers down the central axis (grand silhouette + warm glow).
            MuseumProps.Chandelier(shell.transform, new Vector3(0, H - 0.1f, -hd + 3.4f), st.Sconce);
            MuseumProps.Chandelier(shell.transform, new Vector3(0, H - 0.1f, hd - 3.4f), st.Sconce);
        }

        private static void SideWall(Transform p, string name, float x, float D, float H, float t, Style st, bool door)
        {
            float hd = D * 0.5f;
            if (!door)
            {
                MuseumKit.Box(p, name, new Vector3(x, H * 0.5f, 0), new Vector3(t, H, D), st.Wall, default, true);
                return;
            }
            float doorW = 2.6f, doorH = 3.0f;
            float seg = (D - doorW) * 0.5f;
            MuseumKit.Box(p, name + "_A", new Vector3(x, H * 0.5f, -(doorW * 0.5f + seg * 0.5f)), new Vector3(t, H, seg), st.Wall, default, true);
            MuseumKit.Box(p, name + "_B", new Vector3(x, H * 0.5f, (doorW * 0.5f + seg * 0.5f)), new Vector3(t, H, seg), st.Wall, default, true);
            MuseumKit.Box(p, name + "_Lintel", new Vector3(x, doorH + (H - doorH) * 0.5f, 0), new Vector3(t, H - doorH, doorW), st.Wall, default, true);
            // a hint of a lit room beyond
            MuseumKit.Box(p, name + "_Beyond", new Vector3(x + (x < 0 ? -0.2f : 0.2f), doorH * 0.5f, 0), new Vector3(0.05f, doorH, doorW), st.Sconce);
        }

        private static void Sconce(Transform p, Vector3 pos, float yaw, string glow)
        {
            var s = MuseumKit.Group(p, "Sconce", pos);
            s.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(s.transform, "Bracket", new Vector3(0, -0.15f, 0.05f), new Vector3(0.08f, 0.4f, 0.1f), "brassDark");
            MuseumKit.Box(s.transform, "Lamp", new Vector3(0, 0.05f, 0.12f), new Vector3(0.18f, 0.28f, 0.1f), glow);
        }

        // ----------------------------------------------- GRAND ARCHITECTURE
        // A coffered ceiling field, a grid of recessed panels with rosette bosses,
        // left open over the centre where the laylight sits.
        private static void CofferedCeiling(Transform p, float W, float D, float H, Style st)
        {
            var c = MuseumKit.Group(p, "Coffers", new Vector3(0, H, 0));
            int nx = 5, nz = 4;
            float fieldW = W - 0.6f, fieldD = D - 0.6f;
            float cw = fieldW / nx, cd = fieldD / nz;
            float lw = Mathf.Min(W * 0.34f, 5f), ld = Mathf.Min(D * 0.34f, 4f);
            for (int i = 0; i <= nx; i++)
                MuseumKit.Box(c.transform, "RibX" + i, new Vector3(-fieldW * 0.5f + i * cw, -0.05f, 0), new Vector3(0.14f, 0.14f, fieldD), st.Trim);
            for (int j = 0; j <= nz; j++)
                MuseumKit.Box(c.transform, "RibZ" + j, new Vector3(0, -0.05f, -fieldD * 0.5f + j * cd), new Vector3(fieldW, 0.14f, 0.14f), st.Trim);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float px = -fieldW * 0.5f + (i + 0.5f) * cw;
                    float pz = -fieldD * 0.5f + (j + 0.5f) * cd;
                    if (Mathf.Abs(px) < lw * 0.5f + 0.4f && Mathf.Abs(pz) < ld * 0.5f + 0.4f) continue; // leave the laylight opening
                    MuseumKit.Box(c.transform, "Coffer", new Vector3(px, 0.03f, pz), new Vector3(cw - 0.24f, 0.05f, cd - 0.24f), st.Ceil);
                    MuseumKit.Cyl(c.transform, "Rosette", new Vector3(px, -0.05f, pz), 0.1f, 0.05f, st.Accent);
                }
        }

        // A glowing glass laylight (skylight) with frame + mullion grid, the room's
        // grand light source, on the ceiling underside. Emissive: costs no light.
        private static void CeilingLaylight(Transform p, float W, float D, float H, Style st)
        {
            var l = MuseumKit.Group(p, "Laylight", new Vector3(0, H - 0.06f, 0));
            float lw = Mathf.Min(W * 0.34f, 5f), ld = Mathf.Min(D * 0.34f, 4f);
            MuseumKit.Box(l.transform, "Glass", Vector3.zero, new Vector3(lw, 0.05f, ld), "glowWarm");
            MuseumKit.Box(l.transform, "FrameN", new Vector3(0, 0.04f, ld * 0.5f), new Vector3(lw + 0.3f, 0.14f, 0.16f), "marbleLight");
            MuseumKit.Box(l.transform, "FrameS", new Vector3(0, 0.04f, -ld * 0.5f), new Vector3(lw + 0.3f, 0.14f, 0.16f), "marbleLight");
            MuseumKit.Box(l.transform, "FrameE", new Vector3(lw * 0.5f, 0.04f, 0), new Vector3(0.16f, 0.14f, ld + 0.3f), "marbleLight");
            MuseumKit.Box(l.transform, "FrameW", new Vector3(-lw * 0.5f, 0.04f, 0), new Vector3(0.16f, 0.14f, ld + 0.3f), "marbleLight");
            for (int i = 1; i < 5; i++)
                MuseumKit.Box(l.transform, "MullX" + i, new Vector3(Mathf.Lerp(-lw * 0.5f, lw * 0.5f, i / 5f), 0.02f, 0), new Vector3(0.05f, 0.06f, ld), st.Trim);
            for (int j = 1; j < 4; j++)
                MuseumKit.Box(l.transform, "MullZ" + j, new Vector3(0, 0.02f, Mathf.Lerp(-ld * 0.5f, ld * 0.5f, j / 4f)), new Vector3(lw, 0.06f, 0.05f), st.Trim);
        }

        // A three-tier projecting crown cornice running along a wall (Z wall if
        // isZWall, else an X wall), `coord` being that wall's fixed axis position.
        private static void CrownCornice(Transform p, bool isZWall, float coord, float length, float H, Style st)
        {
            for (int tier = 0; tier < 3; tier++)
            {
                float y = H - 0.16f - tier * 0.13f;
                float proj = 0.12f + tier * 0.07f;
                float thick = 0.16f - tier * 0.03f;
                string mat = tier == 1 ? "marbleLight" : st.Trim;
                Vector3 size = isZWall ? new Vector3(length, thick, proj) : new Vector3(proj, thick, length);
                Vector3 pos = isZWall ? new Vector3(0, y, coord) : new Vector3(coord, y, 0);
                MuseumKit.Box(p, "Crown" + tier, pos, size, mat);
            }
        }

        // Wainscot / dado panelling along the lower long walls (chair rail + recessed panels).
        private static void WallDressing(Transform p, float W, float D, float H, Style st)
        {
            float hw = W * 0.5f, hd = D * 0.5f;
            float dadoH = 1.1f, dadoY = dadoH * 0.5f + 0.05f;
            string panelMat = st.Floor == "concrete" ? "marbleDark" : "woodDark";
            foreach (float z in new[] { hd - 0.16f, -hd + 0.16f })
            {
                MuseumKit.Box(p, "Dado", new Vector3(0, dadoY, z), new Vector3(W - 0.4f, dadoH, 0.05f), panelMat);
                MuseumKit.Box(p, "ChairRail", new Vector3(0, dadoY + dadoH * 0.5f, z), new Vector3(W - 0.3f, 0.08f, 0.1f), st.Trim);
                int np = 6;
                for (int i = 0; i < np; i++)
                {
                    float px = Mathf.Lerp(-hw + 1.2f, hw - 1.2f, (i + 0.5f) / np);
                    MuseumKit.Box(p, "DadoPanel" + i, new Vector3(px, dadoY, z + (z > 0 ? -0.03f : 0.03f)), new Vector3((W - 2.4f) / np - 0.15f, dadoH - 0.25f, 0.02f), st.Wall);
                }
            }
        }

        // A clerestory window band high on the side walls, glowing panes that read
        // as daylight pouring in above the exhibits (emissive; clears the doorways).
        private static void Clerestory(Transform p, float W, float D, float H, Style st)
        {
            float hw = W * 0.5f, hd = D * 0.5f, wy = H - 1.4f;
            foreach (float x in new[] { -hw + 0.16f, hw - 0.16f })
            {
                float into = x < 0 ? 0.08f : -0.08f;
                for (int i = 0; i < 3; i++)
                {
                    float z = Mathf.Lerp(-hd + 1.9f, hd - 1.9f, i / 2f);
                    if (Mathf.Abs(z) < 1.7f) continue; // clear the doorway
                    MuseumKit.Box(p, "Clerestory", new Vector3(x, wy, z), new Vector3(0.1f, 1.1f, 1.3f), st.Sconce);
                    MuseumKit.Box(p, "ClFrame", new Vector3(x + into, wy, z), new Vector3(0.06f, 1.25f, 1.45f), st.Trim);
                    MuseumKit.Box(p, "ClMullV", new Vector3(x + into, wy, z), new Vector3(0.05f, 1.1f, 0.05f), st.Trim);
                    MuseumKit.Box(p, "ClMullH", new Vector3(x + into, wy, z), new Vector3(0.05f, 0.05f, 1.3f), st.Trim);
                }
            }
        }

        // A classical entablature over a colonnade: architrave + a triglyph/metope
        // frieze + a dentilled cornice. `yaw` turns the detailing toward the room.
        private static void Entablature(Transform p, Vector3 pos, float length, float yaw, Style st)
        {
            var e = MuseumKit.Group(p, "Entablature", pos);
            e.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(e.transform, "Architrave", new Vector3(0, 0f, 0), new Vector3(length, 0.22f, 0.5f), "marbleLight");
            MuseumKit.Box(e.transform, "FriezeBand", new Vector3(0, 0.28f, 0), new Vector3(length, 0.34f, 0.42f), "marbleLight");
            int units = Mathf.Max(6, Mathf.RoundToInt(length / 1.1f));
            for (int i = 0; i <= units; i++)
            {
                float fx = Mathf.Lerp(-length * 0.5f + 0.4f, length * 0.5f - 0.4f, i / (float)units);
                MuseumKit.Box(e.transform, "Triglyph" + i, new Vector3(fx, 0.28f, 0.22f), new Vector3(0.13f, 0.32f, 0.04f), st.Trim);
                if (i < units)
                {
                    float mx = Mathf.Lerp(-length * 0.5f + 0.4f, length * 0.5f - 0.4f, (i + 0.5f) / units);
                    MuseumKit.Box(e.transform, "Metope" + i, new Vector3(mx, 0.28f, 0.22f), new Vector3(length / units * 0.55f, 0.26f, 0.02f), st.Accent);
                }
            }
            MuseumKit.Box(e.transform, "EntCornice", new Vector3(0, 0.54f, 0.06f), new Vector3(length, 0.14f, 0.6f), "marbleLight");
            int dent = Mathf.Max(12, Mathf.RoundToInt(length / 0.45f));
            for (int i = 0; i < dent; i++)
            {
                float dx = Mathf.Lerp(-length * 0.5f + 0.25f, length * 0.5f - 0.25f, i / (float)(dent - 1));
                MuseumKit.Box(e.transform, "Dentil" + i, new Vector3(dx, 0.45f, 0.26f), new Vector3(0.07f, 0.08f, 0.1f), st.Trim);
            }
        }

        // A grand compass-rose marble inlay set into the floor beneath the hero.
        private static void FloorInlay(Transform p, Vector3 at, Style st)
        {
            var f = MuseumKit.Group(p, "FloorInlay", at);
            string fieldMat = st.Floor == "marbleDark" ? "marbleLight" : "marbleDark";
            MuseumKit.Cyl(f.transform, "Ring1", new Vector3(0, 0.000f, 0), 2.5f, 0.012f, st.Trim);
            MuseumKit.Cyl(f.transform, "Ring2", new Vector3(0, 0.002f, 0), 2.2f, 0.012f, st.Accent);
            MuseumKit.Cyl(f.transform, "Field", new Vector3(0, 0.004f, 0), 2.0f, 0.012f, fieldMat);
            MuseumKit.Cyl(f.transform, "Hub", new Vector3(0, 0.006f, 0), 0.9f, 0.012f, st.Accent);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                MuseumKit.Box(f.transform, "Point" + i, new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 1.45f, 0.008f, Mathf.Sin(a * Mathf.Deg2Rad) * 1.45f),
                    new Vector3(0.13f, 0.012f, i % 2 == 0 ? 1.4f : 0.7f), fieldMat, new Vector3(0, -a, 0));
            }
        }

        // ---------------------------------------------------------------- HERO
        // The player spawns at the room centre (the room's PlayerAnchor) facing +Z,
        // so the hero sits ahead of them toward the back of the hall, never on the
        // spawn point, with its plaque on the near (visitor) side.
        private const float HeroZ = 2.0f;

        private static void BuildHero(Transform p, Gallery g, Style st)
        {
            // The entrance portal authors its own world-spanning layout from the
            // room origin; every other hero sits forward on its plinth at HeroZ.
            var hero = MuseumKit.Group(p, "Hero", new Vector3(0, 0, g.Key == "entrance" ? 0f : HeroZ));
            switch (g.Key)
            {
                case "entrance": HeroPortal(hero.transform, g); break;
                case "atrium": HeroArmillary(hero.transform, g); break;
                case "ancient": HeroCipherDisk(hero.transform, g); break;
                case "wwii": HeroEnigma(hero.transform, g); break;
                case "vault": HeroVault(hero.transform, g); break;
                case "reveal": HeroSculpture(hero.transform, g); break;
                case "complete": HeroComplete(hero.transform, g); break;
            }

            // Hero plaque (near side, facing the visitor) + rope barrier + benches.
            if (g.Key != "entrance")
            {
                // The completion hall presents the closing plaque on its grand panel
                // (HeroComplete), so it skips the small near-side plaque to avoid a
                // duplicate of the same verbatim text.
                if (g.Key != "complete")
                    MuseumProps.Plaque(hero.transform, new Vector3(0, 1.15f, -1.5f), 180f, g.HeroTitle, g.HeroPlaque, 0.95f, true);
                MuseumProps.RopeBarrier(hero.transform, "Hero", new[]
                {
                    new Vector3(-1.5f, 0, 1.5f), new Vector3(1.5f, 0, 1.5f),
                    new Vector3(1.5f, 0, -1.5f), new Vector3(-1.5f, 0, -1.5f),
                });
                MuseumProps.Bench(p, new Vector3(0, 0, -2.8f), 0f, "marbleLight");    // behind visitor, faces hero
                MuseumProps.Bench(p, new Vector3(-5.0f, 0, HeroZ), 90f, "marbleLight"); // side, faces hero
            }

            // Anchor where a fully-wired interactive exhibit can be dropped later.
            MuseumKit.Group(p, "HERO_ANCHOR_DropInteractiveExhibitHere", new Vector3(0, 1.0f, HeroZ));
        }

        private static void HeroPlinth(Transform p, float top, string key = "marbleLight")
        {
            MuseumKit.Box(p, "PlinthFoot", new Vector3(0, 0.06f, 0), new Vector3(2.0f, 0.12f, 2.0f), "marbleDark", default, true);
            MuseumKit.Box(p, "Plinth", new Vector3(0, top * 0.5f + 0.12f, 0), new Vector3(1.5f, top, 1.5f), key, default, true);
            MuseumKit.Box(p, "PlinthCap", new Vector3(0, top + 0.15f, 0), new Vector3(1.7f, 0.08f, 1.7f), "marbleDark");
        }

        private static void HeroCipherDisk(Transform p, Gallery g)
        {
            HeroPlinth(p, 0.95f, "sandstone");
            var turn = MuseumKit.Group(p, "Disk", new Vector3(0, 1.5f, 0));
            turn.transform.localEulerAngles = new Vector3(90, 0, 0);
            MuseumKit.Cyl(turn.transform, "Outer", Vector3.zero, 0.95f, 0.1f, "bronze");
            var inner = MuseumKit.Cyl(turn.transform, "Inner", new Vector3(0, 0.06f, 0), 0.66f, 0.12f, "copper");
            MuseumKit.Cyl(turn.transform, "Hub", new Vector3(0, 0.14f, 0), 0.12f, 0.08f, "gold");
            string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * 360f * Mathf.Deg2Rad;
                MuseumKit.Box(turn.transform, "Tick" + i, new Vector3(Mathf.Cos(a) * 0.86f, 0.06f, Mathf.Sin(a) * 0.86f), new Vector3(0.02f, 0.04f, 0.06f), "bronze", new Vector3(0, -i / 26f * 360f, 0));
                if (i % 2 == 0)
                    MuseumKit.Label(turn.transform, "L" + i, new Vector3(Mathf.Cos(a) * 0.78f, 0.12f, Mathf.Sin(a) * 0.78f), new Vector2(0.12f, 0.12f),
                        letters[i].ToString(), MuseumKit.TextRole.Caption, MuseumKit.Ink, TextAlignmentOptions.Center, new Vector3(90, -i / 26f * 360f, 0));
            }
            inner.AddComponent<Decrypted.Visuals.MuseumSpin>().DegreesPerSecond = 6f;
        }

        private static void HeroEnigma(Transform p, Gallery g)
        {
            HeroPlinth(p, 0.9f, "ironDark");
            var m = MuseumKit.Group(p, "EnigmaModel", new Vector3(0, 1.15f, 0));
            MuseumKit.Box(m.transform, "Body", new Vector3(0, 0.18f, 0), new Vector3(1.3f, 0.36f, 0.95f), "woodDark", default, false);
            MuseumKit.Box(m.transform, "Panel", new Vector3(0, 0.34f, 0.18f), new Vector3(1.2f, 0.06f, 0.55f), "ironDark", new Vector3(-12, 0, 0));
            // three rotors
            for (int i = 0; i < 3; i++)
            {
                MuseumKit.Cyl(m.transform, "Rotor" + i, new Vector3((i - 1) * 0.26f, 0.46f, -0.28f), 0.12f, 0.18f, "brass", new Vector3(0, 0, 90));
                MuseumKit.Label(m.transform, "RotorL" + i, new Vector3((i - 1) * 0.26f, 0.58f, -0.28f), new Vector2(0.1f, 0.08f), "ABC"[i].ToString(), MuseumKit.TextRole.Caption, MuseumKit.Cream, TextAlignmentOptions.Center);
            }
            // lampboard glow + keys
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 6; c++)
                {
                    MuseumKit.Cyl(m.transform, $"Lamp{r}{c}", new Vector3((c - 2.5f) * 0.16f, 0.4f, 0.05f - r * 0.14f), 0.04f, 0.02f, (r == 0 && c == 3) ? "lamp" : "ironDark", new Vector3(90, 0, 0));
                    MuseumKit.Cyl(m.transform, $"Key{r}{c}", new Vector3((c - 2.5f) * 0.16f, 0.36f, 0.34f - r * 0.14f), 0.045f, 0.04f, "blackMatte", new Vector3(90, 0, 0));
                }
            MuseumKit.Box(m.transform, "Lever", new Vector3(0.72f, 0.4f, 0), new Vector3(0.06f, 0.3f, 0.06f), "steel", new Vector3(0, 0, -20));
        }

        private static void HeroVault(Transform p, Gallery g)
        {
            MuseumKit.Box(p, "Wall", new Vector3(0, 1.6f, -0.5f), new Vector3(4.2f, 3.2f, 0.4f), "concrete", default, true);
            MuseumKit.Box(p, "Frame", new Vector3(0, 1.5f, -0.28f), new Vector3(2.7f, 2.7f, 0.3f), "steel");
            var door = MuseumKit.Group(p, "VaultDoor", new Vector3(0, 1.5f, -0.12f));
            MuseumKit.Cyl(door.transform, "Door", Vector3.zero, 1.15f, 0.2f, "chrome", new Vector3(90, 0, 0));
            MuseumKit.Cyl(door.transform, "Ring", new Vector3(0, 0, 0.12f), 1.0f, 0.06f, "steel", new Vector3(90, 0, 0));
            var wheel = MuseumKit.Group(door.transform, "Wheel", new Vector3(0, 0, 0.2f));
            MuseumKit.Cyl(wheel.transform, "Hub", Vector3.zero, 0.22f, 0.1f, "ironDark", new Vector3(90, 0, 0));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                MuseumKit.Box(wheel.transform, "Spoke" + i, new Vector3(Mathf.Cos(a) * 0.36f, Mathf.Sin(a) * 0.36f, 0), new Vector3(0.1f, 0.7f, 0.08f), "steel", new Vector3(0, 0, i * 90f));
            }
            wheel.AddComponent<Decrypted.Visuals.MuseumSpin>().DegreesPerSecond = 5f;
            // bolts
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                MuseumKit.Cyl(door.transform, "Bolt" + i, new Vector3(Mathf.Cos(a) * 1.05f, Mathf.Sin(a) * 1.05f, 0.06f), 0.05f, 0.12f, "ironDark", new Vector3(90, 0, 0));
            }
            MuseumKit.Box(p, "StatusGreen", new Vector3(0.9f, 2.7f, 0.0f), new Vector3(0.18f, 0.18f, 0.06f), "glowGreen");
            MuseumKit.Box(p, "ArchiveGlow", new Vector3(0, 1.5f, -0.45f), new Vector3(1.6f, 1.6f, 0.05f), "glowCyan");
        }

        private static void HeroSculpture(Transform p, Gallery g)
        {
            HeroPlinth(p, 0.8f, "marbleDark");
            var s = MuseumKit.Group(p, "Synthesis", new Vector3(0, 1.0f, 0));
            // Roman stele base → clockwork gear → circuit lattice, stacked & lit
            MuseumKit.Box(s.transform, "Stele", new Vector3(0, 0.35f, 0), new Vector3(0.5f, 0.7f, 0.18f), "sandstone");
            MuseumKit.Cyl(s.transform, "Gear", new Vector3(0, 0.85f, 0), 0.34f, 0.12f, "brass");
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                MuseumKit.Box(s.transform, "Tooth" + i, new Vector3(Mathf.Cos(a) * 0.4f, 0.85f, Mathf.Sin(a) * 0.4f), new Vector3(0.08f, 0.1f, 0.08f), "brass", new Vector3(0, -i * 36f, 0));
            }
            var lat = MuseumKit.Group(s.transform, "Lattice", new Vector3(0, 1.35f, 0));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                MuseumKit.Sphere(lat.transform, "Node" + i, new Vector3(Mathf.Cos(a) * 0.25f, (i % 2) * 0.2f, Mathf.Sin(a) * 0.25f), 0.12f, "glowCyan");
            }
            MuseumKit.Sphere(lat.transform, "Core", Vector3.zero, 0.18f, "glowCool");
            s.AddComponent<Decrypted.Visuals.MuseumSpin>().DegreesPerSecond = 8f;
        }

        private static void HeroArmillary(Transform p, Gallery g)
        {
            HeroPlinth(p, 1.0f, "marbleLight");
            var globe = MuseumKit.Group(p, "Armillary", new Vector3(0, 1.85f, 0));
            MuseumKit.Sphere(globe.transform, "Globe", Vector3.zero, 0.9f, "carpetBlue");
            for (int i = 0; i < 6; i++)
                MuseumKit.Box(globe.transform, "Land" + i, new Vector3(Mathf.Cos(i) * 0.4f, Mathf.Sin(i * 1.7f) * 0.4f, Mathf.Sin(i) * 0.35f), new Vector3(0.3f, 0.18f, 0.3f), "sandstone");
            for (int i = 0; i < 3; i++)
                MuseumKit.Cyl(globe.transform, "Ring" + i, Vector3.zero, 0.62f, 0.02f, "brass", new Vector3(i * 60f, i * 40f, 0));
            globe.AddComponent<Decrypted.Visuals.MuseumSpin>().DegreesPerSecond = 5f;
            MuseumKit.Box(p, "AxisGlow", new Vector3(0, 1.85f, 0), new Vector3(0.04f, 2.0f, 0.04f), "glowGold", new Vector3(20, 0, 0));
        }

        private static void HeroPortal(Transform p, Gallery g)
        {
            // A grand glowing entrance ring + title wall + floor seal that says "press to begin".
            MuseumProps.WallSign(p, new Vector3(0, 4.4f, 5.7f), 180f, MuseumContent.MuseumName, MuseumContent.MuseumTagline, "brass");
            var ring = MuseumKit.Group(p, "Portal", new Vector3(0, 2.2f, 4.2f));
            int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * 360f * Mathf.Deg2Rad;
                MuseumKit.Box(ring.transform, "Seg" + i, new Vector3(Mathf.Cos(a) * 1.6f, Mathf.Sin(a) * 1.6f, 0), new Vector3(0.45f, 0.18f, 0.18f), "glowGold", new Vector3(0, 0, Mathf.Rad2Deg * a));
            }
            ring.AddComponent<Decrypted.Visuals.MuseumSpin>().Axis = Vector3.forward;
            ring.GetComponent<Decrypted.Visuals.MuseumSpin>().DegreesPerSecond = 4f;
            MuseumKit.Label(p, "Mission", new Vector3(0, 2.2f, 4.05f), new Vector2(2.4f, 1.4f), MuseumContent.Mission,
                MuseumKit.TextRole.Body, MuseumKit.Cream, TextAlignmentOptions.Center);
            MuseumProps.FloorMedallion(p, new Vector3(0, 0, 1.5f), 1.1f, MuseumContent.MuseumName);
            MuseumProps.Plaque(p, new Vector3(0, 1.1f, 1.9f), 0f, g.HeroTitle, g.HeroPlaque, 0.95f, true);
        }

        // The payoff centerpiece: a grand freestanding panel carrying the verbatim
        // closing plaque, on a low wide plinth, with a warm emissive backing fill so
        // it reads cleanly without any new realtime light. Faces the visitor (-Z).
        private static void HeroComplete(Transform p, Gallery g)
        {
            HeroPlinth(p, 0.35f, "marbleLight");

            // Sized to sit comfortably under the 4.25 m completion-hall ceiling.
            var panel = MuseumKit.Group(p, "ClosingPanel", new Vector3(0, 0, 0.1f));
            MuseumKit.Box(panel.transform, "Back", new Vector3(0, 1.7f, 0.10f), new Vector3(4.2f, 2.6f, 0.18f), "marbleDark");
            MuseumKit.Box(panel.transform, "FrameOuter", new Vector3(0, 1.7f, 0.02f), new Vector3(4.6f, 2.9f, 0.10f), "gold");
            MuseumKit.Box(panel.transform, "FrameInner", new Vector3(0, 1.7f, -0.01f), new Vector3(4.1f, 2.5f, 0.08f), "brass");
            // Warm emissive fill behind the text, a subtle glow, zero light cost.
            MuseumKit.Box(panel.transform, "WarmFill", new Vector3(0, 1.7f, 0.0f), new Vector3(3.9f, 2.3f, 0.04f), "glowWarm");

            // Museum name + the closing plaque (the payoff moment).
            MuseumKit.Label(panel.transform, "Title", new Vector3(0, 2.7f, -0.04f), new Vector2(3.8f, 0.55f),
                "DECRYPTED", MuseumKit.TextRole.Sign, MuseumKit.BrassText, TextAlignmentOptions.Center, new Vector3(0, 180f, 0));
            MuseumKit.Label(panel.transform, "Plaque", new Vector3(0, 1.5f, -0.04f), new Vector2(3.6f, 1.7f),
                g.HeroPlaque, MuseumKit.TextRole.Heading, MuseumKit.WarmWhite, TextAlignmentOptions.Center, new Vector3(0, 180f, 0));
        }

        // ------------------------------------------------------------- PERIMETER
        private static void DressPerimeter(Transform p, Gallery g, Style st)
        {
            var dress = MuseumKit.Group(p, "Exhibits");
            float hw = st.W * 0.5f, hd = st.D * 0.5f;

            var floorSlots = FloorSlots(hw, hd);
            var wallSlots = WallSlots(hw, hd, st.H);
            int fi = 0, wi = 0;

            // Secondary exhibits → furniture by kind.
            foreach (var ex in g.Exhibits)
            {
                if (ex.Kind == CaseKind.WallPanel || ex.Kind == CaseKind.Relief)
                {
                    if (wi < wallSlots.Count) { var s = wallSlots[wi++]; MuseumProps.PlaceExhibit(dress.transform, s.pos, s.yaw, ex); }
                    else { var s = floorSlots[fi++ % floorSlots.Count]; MuseumProps.PlaceExhibit(dress.transform, s.pos, s.yaw, ex); }
                }
                else
                {
                    if (fi < floorSlots.Count) { var s = floorSlots[fi++]; MuseumProps.PlaceExhibit(dress.transform, s.pos, s.yaw, ex); }
                    else { var s = wallSlots[wi++ % wallSlots.Count]; MuseumProps.PlaceExhibit(dress.transform, s.pos, s.yaw, ex); }
                }
            }

            // Portrait gallery of pioneers along remaining wall slots.
            foreach (var fig in g.Figures)
            {
                if (wi >= wallSlots.Count) break;
                var s = wallSlots[wi++];
                MuseumProps.Portrait(dress.transform, s.pos, s.yaw, fig);
            }

            // Wall timeline on the upper back wall.
            if (g.Timeline != null && g.Timeline.Length > 0)
                MuseumProps.TimelineStrip(dress.transform, new Vector3(0, st.H - 1.6f, hd - 0.12f), 180f, Mathf.Min(st.W - 2f, 12f), g.Timeline, "TIMELINE");

            // Fill any leftover wall slots with framed principle/quote panels so no wall is blank.
            string[] fillers = {
                "“The enemy knows the system. Keep the key.”",
                "Cryptography  ·  the art of making secrets.",
                "Cryptanalysis  ·  the art of breaking them.",
                "Every code ever made has met a mind clever enough to break it, so far.",
            };
            int fcount = 0;
            while (wi < wallSlots.Count && fcount < fillers.Length)
            {
                var s = wallSlots[wi++];
                MuseumProps.WallPanel(dress.transform, s.pos, s.yaw, new Vector2(1.2f, 0.8f),
                    g.Name, fillers[fcount++], "brass", "none");
            }
        }

        // Floor furniture anchors: a ring just inside the long walls, facing in.
        private static List<(Vector3 pos, float yaw)> FloorSlots(float hw, float hd)
        {
            var list = new List<(Vector3, float)>();
            // Rows set well inside the colonnade (which hugs the long walls).
            float zb = hd - 2.4f, zf = -hd + 2.4f;
            float[] xs = { -hw + 2.4f, -hw + 5.0f, hw - 5.0f, hw - 2.4f };
            foreach (var x in xs) list.Add((new Vector3(x, 0, zb), 180f)); // back wall, face -Z
            foreach (var x in xs) list.Add((new Vector3(x, 0, zf), 0f));   // front wall, face +Z
            // corners near side walls (avoid the doorways at z≈0)
            list.Add((new Vector3(-hw + 1.4f, 0, hd - 3.4f), 90f));
            list.Add((new Vector3(hw - 1.4f, 0, hd - 3.4f), 270f));
            list.Add((new Vector3(-hw + 1.4f, 0, -hd + 3.4f), 90f));
            list.Add((new Vector3(hw - 1.4f, 0, -hd + 3.4f), 270f));
            return list;
        }

        // Wall-mounted anchors at eye height on all four walls (skipping doorways).
        private static List<(Vector3 pos, float yaw)> WallSlots(float hw, float hd, float H)
        {
            var list = new List<(Vector3, float)>();
            float y = 2.0f;
            float[] xs = { -hw + 2.6f, 0f, hw - 2.6f };
            foreach (var x in xs) list.Add((new Vector3(x, y, hd - 0.16f), 180f)); // back wall
            foreach (var x in xs) list.Add((new Vector3(x, y, -hd + 0.16f), 0f));  // front wall
            // side walls: above/below the doorway → use the segments away from centre
            list.Add((new Vector3(-hw + 0.16f, y, hd - 2.8f), 90f));
            list.Add((new Vector3(-hw + 0.16f, y, -hd + 2.8f), 90f));
            list.Add((new Vector3(hw - 0.16f, y, hd - 2.8f), 270f));
            list.Add((new Vector3(hw - 0.16f, y, -hd + 2.8f), 270f));
            return list;
        }

        // ------------------------------------------------------- SIGNAGE & DECOR
        private static void BuildSignageAndDecor(Transform p, Gallery g, Style st)
        {
            var deco = MuseumKit.Group(p, "Signage");
            float hw = st.W * 0.5f, hd = st.D * 0.5f, H = st.H;
            // Desk galleries keep the left-front for an info desk, so their directory
            // goes right-front; kiosk galleries do the opposite.
            bool hasDesk = g.Key == "atrium" || g.Key == "entrance";

            // Hanging gallery sign near the centre, and a gallery sign over the back wall.
            MuseumProps.HangingSign(deco.transform, new Vector3(0, H - 1.0f, -hd + 2.6f), g.Name);
            MuseumProps.WallSign(deco.transform, new Vector3(0, H - 0.9f, hd - 0.14f), 180f, g.Name, g.Subtitle, st.Accent);

            // Orientation plaque flush on the entrance wall.
            MuseumProps.Plaque(deco.transform, new Vector3(hw - 2.4f, 1.6f, -hd + 0.18f), 0f, "ABOUT THIS GALLERY", g.Intro, 0.95f, true);

            // Directory near the entrance, on whichever front corner is free.
            if (g.Directory != null && g.Directory.Length > 0)
            {
                float dx = hasDesk ? (hw - 1.8f) : (-hw + 1.8f);
                float dyaw = hasDesk ? -55f : 55f;
                MuseumProps.DirectorySign(deco.transform, new Vector3(dx, 0, -hd + 2.2f), dyaw, g.Name, g.Directory);
            }

            // Exit / entry signage over the doorways.
            if (g.State != MuseumState.RevealChamber)
                MuseumProps.ExitSign(deco.transform, new Vector3(hw - 0.35f, 3.2f, 0), 270f, "NEXT  ►");
            if (g.State != MuseumState.Splash)
                MuseumProps.ExitSign(deco.transform, new Vector3(-hw + 0.35f, 3.2f, 0), 90f, "◄  BACK");

            // Banners hanging from the ceiling.
            MuseumProps.Banner(deco.transform, new Vector3(-hw + 3.0f, H - 0.5f, 0), new Vector2(1.0f, 2.2f), g.Name, "velvetRed");
            MuseumProps.Banner(deco.transform, new Vector3(hw - 3.0f, H - 0.5f, 0), new Vector2(1.0f, 2.2f), MuseumContent.MuseumTagline, "feltGreen");

            // Planters in the corners.
            MuseumProps.Planter(deco.transform, new Vector3(-hw + 1.0f, 0, hd - 1.0f), 1.1f);
            MuseumProps.Planter(deco.transform, new Vector3(hw - 1.0f, 0, hd - 1.0f), 1.1f);

            // Facilities: bin + donation near the entrance, cameras in the upper corners.
            MuseumProps.TrashBin(deco.transform, new Vector3(-hw + 1.0f, 0, -hd + 1.2f));
            MuseumProps.SecurityCamera(deco.transform, new Vector3(hw - 0.4f, H - 0.5f, hd - 0.4f), 225f);
            MuseumProps.SecurityCamera(deco.transform, new Vector3(-hw + 0.4f, H - 0.5f, hd - 0.4f), 135f);

            // Entrance & atrium get a staffed information desk (left of the entry);
            // the content galleries get an interactive guide kiosk (right of the entry).
            if (hasDesk)
                MuseumProps.InfoDesk(deco.transform, new Vector3(-hw + 3.4f, 0, -hd + 2.6f), 150f);
            else
                MuseumProps.Kiosk(deco.transform, new Vector3(hw - 2.6f, 0, -hd + 2.2f), -35f, g.Name,
                    g.Subtitle + "\n\nLean in to any case for the full story.");
            if (g.Key == "entrance")
                MuseumProps.DonationBox(deco.transform, new Vector3(hw - 1.4f, 0, -hd + 1.2f));
        }

        // ------------------------------------------------------------- LIGHTING
        private static void BuildLighting(Transform p, Gallery g, Style st)
        {
            var lights = MuseumKit.Group(p, "Lighting");
            float hw = st.W * 0.5f, hd = st.D * 0.5f, H = st.H;

            // The completion hall is BAKED-ONLY: no new realtime lights. Its glow comes
            // from the emissive laylight, sconces, clerestory, chandeliers and the warm
            // panel fill (all zero-cost). We add a light probe group so the dynamic
            // player and hands pick up the baked bounce. (Bake via Window > Rendering >
            // Lighting > Generate Lighting for the final look.)
            if (g.Key == "complete")
            {
                var probesGO = MuseumKit.Group(lights.transform, "LightProbes");
                var lpg = probesGO.AddComponent<LightProbeGroup>();
                var pts = new List<Vector3>();
                float pxw = hw - 1.2f, pzd = hd - 1.2f;
                foreach (float x in new[] { -pxw, -pxw * 0.4f, pxw * 0.4f, pxw })
                    foreach (float z in new[] { -pzd, 0f, pzd })
                        foreach (float y in new[] { 0.4f, 1.6f, 3.0f })
                            pts.Add(new Vector3(x, y, z));
                lpg.probePositions = pts.ToArray();
                return;
            }

            // Two hero spots from the ceiling, flanking and aimed at the centerpiece.
            MuseumKit.Spot(lights.transform, "HeroSpotA", new Vector3(-1.4f, H - 0.3f, HeroZ + 1.0f), new Vector3(60, 200, 0), st.Key, st.Intensity, 12f, 42f, false);
            MuseumKit.Spot(lights.transform, "HeroSpotB", new Vector3(1.4f, H - 0.3f, HeroZ - 1.0f), new Vector3(60, 20, 0), st.Key, st.Intensity * 0.8f, 12f, 46f, false);

            // Soft ambient fill near the ceiling so the room is never black before baking.
            MuseumKit.Point(lights.transform, "FillFront", new Vector3(0, H - 0.6f, -hd + 3f), st.Key, 2.2f, 14f);
            MuseumKit.Point(lights.transform, "FillBack", new Vector3(0, H - 0.6f, hd - 3f), st.Key, 2.2f, 14f);

            // A grazing wash for the portrait / exhibit wall.
            MuseumKit.Spot(lights.transform, "WallWash", new Vector3(0, H - 0.4f, hd - 2.4f), new Vector3(35, 180, 0), st.Key, 6f, 12f, 70f, false);
        }

        // =====================================================================
        //  Scene lookup
        // =====================================================================
        private static Transform FindRoom(MuseumState state)
        {
            string target = "Room_" + state;
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindRecursive(root.transform, target);
                if (t != null) return t;
            }
            return null;
        }

        private static Transform FindRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindRecursive(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
