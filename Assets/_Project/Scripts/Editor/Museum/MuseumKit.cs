// -----------------------------------------------------------------------------
//  MuseumKit.cs   (Editor)
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  The low-level construction toolkit shared by the whole museum-dressing
//  pipeline. It owns three things:
//
//    1. A PALETTE of shared URP materials (created once as real .mat assets so
//       they batch under the SRP Batcher and survive rebuilds).
//    2. PRIMITIVE builders (box / cylinder / quad / sphere) that reuse Unity's
//       built-in meshes — no per-object mesh allocation.
//    3. World-space TEXT (TextMeshPro) and LIGHT helpers tuned for VR legibility.
//
//  Everything is authored in METRES in a parent's local space. Higher-level props
//  live in MuseumProps.cs; the per-gallery layout lives in MuseumBuilder.cs.
//
//  Text sizing note: world-space TMP point-size does not map cleanly to metres,
//  so every label uses TMP auto-sizing constrained to a metre-sized box. That
//  makes legibility robust regardless of the exact font metrics.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Decrypted.EditorTools
{
    public static class MuseumKit
    {
        // ---- Asset locations ------------------------------------------------
        public const string GenRoot = "Assets/_Project/Art/Generated/Museum";
        public const string MatDir = GenRoot + "/Materials";

        // ---- Shared mesh cache ---------------------------------------------
        private static Mesh _cube, _cyl, _quad, _sphere;
        private static Shader _lit;
        private static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        // =====================================================================
        //  Lifecycle
        // =====================================================================
        public static void Init()
        {
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) _lit = Shader.Find("Standard");
            CacheMeshes();
            EnsureFolder(MatDir);
            _mats.Clear(); // re-resolve material assets fresh each build
        }

        private static void CacheMeshes()
        {
            if (_cube != null) return;
            _cube = Prim(PrimitiveType.Cube);
            _cyl = Prim(PrimitiveType.Cylinder);
            _quad = Prim(PrimitiveType.Quad);
            _sphere = Prim(PrimitiveType.Sphere);
        }

        private static Mesh Prim(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return m;
        }

        // =====================================================================
        //  Palette
        // =====================================================================
        private struct MatDef
        {
            public Color col; public float metal; public float smooth;
            public bool transparent; public bool emissive; public Color emission;
        }

        public static Material Mat(string key)
        {
            if (_mats.TryGetValue(key, out var cached) && cached != null) return cached;

            string path = MatDir + "/M_" + key + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { _mats[key] = existing; return existing; }

            var def = Def(key);
            var m = new Material(_lit) { name = "M_" + key };
            Configure(m, def);
            AssetDatabase.CreateAsset(m, path);
            _mats[key] = m;
            return m;
        }

        private static void Configure(Material m, MatDef d)
        {
            // Set both URP and Standard property names so the fallback still reads.
            m.SetColor("_BaseColor", d.col);
            m.SetColor("_Color", d.col);
            m.SetFloat("_Metallic", d.metal);
            m.SetFloat("_Smoothness", d.smooth);
            m.SetFloat("_Glossiness", d.smooth);

            if (d.emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", d.emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (d.transparent)
            {
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_Surface", 1f);          // URP: transparent
                m.SetFloat("_Blend", 0f);            // alpha blend
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", (float)CullMode.Off);   // see through both faces
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private static Color RGB(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        private static MatDef Solid(Color c, float metal, float smooth) =>
            new MatDef { col = c, metal = metal, smooth = smooth };

        private static MatDef Glow(Color c, Color e) =>
            new MatDef { col = c, metal = 0f, smooth = 0.4f, emissive = true, emission = e };

        private static MatDef Def(string key)
        {
            switch (key)
            {
                // --- stone / plaster / architecture ---
                case "stone":        return Solid(RGB(150, 142, 126), 0f, 0.18f);
                case "sandstone":    return Solid(RGB(196, 176, 140), 0f, 0.15f);
                case "marbleLight":  return Solid(RGB(228, 224, 214), 0f, 0.62f);
                case "marbleDark":   return Solid(RGB(46, 47, 54), 0f, 0.66f);
                case "plasterWarm":  return Solid(RGB(160, 150, 132), 0f, 0.20f);
                case "plasterCool":  return Solid(RGB(132, 140, 150), 0f, 0.26f);
                case "plasterNeutral": return Solid(RGB(150, 148, 144), 0f, 0.22f);
                case "concrete":     return Solid(RGB(64, 64, 70), 0f, 0.20f);
                case "concreteLight":return Solid(RGB(120, 120, 126), 0f, 0.24f);
                case "ceiling":      return Solid(RGB(40, 40, 46), 0f, 0.30f);
                case "trimDark":     return Solid(RGB(26, 26, 30), 0.25f, 0.5f);

                // --- metals ---
                case "brass":        return Solid(RGB(196, 154, 72), 0.9f, 0.74f);
                case "brassDark":    return Solid(RGB(120, 92, 44), 0.85f, 0.6f);
                case "bronze":       return Solid(RGB(120, 86, 52), 0.85f, 0.55f);
                case "copper":       return Solid(RGB(168, 96, 64), 0.9f, 0.62f);
                case "gold":         return Solid(RGB(212, 172, 92), 1f, 0.82f);
                case "steel":        return Solid(RGB(142, 146, 152), 0.85f, 0.7f);
                case "ironDark":     return Solid(RGB(48, 50, 56), 0.7f, 0.4f);
                case "chrome":       return Solid(RGB(196, 200, 206), 1f, 0.9f);

                // --- woods / fabric ---
                case "wood":         return Solid(RGB(96, 64, 40), 0f, 0.35f);
                case "woodDark":     return Solid(RGB(58, 38, 26), 0f, 0.3f);
                case "carpetRed":    return Solid(RGB(96, 28, 30), 0f, 0.1f);
                case "carpetBlue":   return Solid(RGB(30, 40, 78), 0f, 0.1f);
                case "velvetRed":    return Solid(RGB(120, 26, 32), 0f, 0.12f);
                case "feltGreen":    return Solid(RGB(28, 64, 48), 0f, 0.1f);
                case "leaf":         return Solid(RGB(46, 86, 52), 0f, 0.2f);

                // --- documents / glass ---
                case "paperCream":   return Solid(RGB(214, 202, 174), 0f, 0.1f);
                case "paperAged":    return Solid(RGB(188, 168, 130), 0f, 0.1f);
                case "glass":        return new MatDef { col = new Color(0.62f, 0.74f, 0.78f, 0.16f), metal = 0f, smooth = 0.96f, transparent = true };
                case "blackMatte":   return Solid(RGB(18, 18, 20), 0f, 0.2f);
                case "whiteMatte":   return Solid(RGB(222, 222, 224), 0f, 0.3f);

                // --- emissive accents (free light, no bake) ---
                case "glowWarm":     return Glow(RGB(60, 46, 28), new Color(1.7f, 1.15f, 0.55f));
                case "glowAmber":    return Glow(RGB(60, 40, 18), new Color(1.9f, 1.0f, 0.35f));
                case "glowGold":     return Glow(RGB(60, 50, 24), new Color(1.6f, 1.25f, 0.6f));
                case "glowCool":     return Glow(RGB(20, 40, 50), new Color(0.35f, 1.2f, 1.6f));
                case "glowCyan":     return Glow(RGB(16, 44, 50), new Color(0.3f, 1.5f, 1.8f));
                case "glowGreen":    return Glow(RGB(18, 44, 26), new Color(0.35f, 1.7f, 0.6f));
                case "glowRed":      return Glow(RGB(48, 16, 16), new Color(1.8f, 0.3f, 0.3f));
                case "glowBlue":     return Glow(RGB(18, 26, 50), new Color(0.4f, 0.6f, 1.9f));
                case "screen":       return Glow(RGB(14, 24, 34), new Color(0.45f, 0.95f, 1.35f));
                case "lamp":         return Glow(RGB(60, 52, 30), new Color(2.0f, 1.5f, 0.7f));

                default:             return Solid(RGB(140, 140, 144), 0f, 0.3f);
            }
        }

        // =====================================================================
        //  Primitives  (all in parent-local metres)
        // =====================================================================
        public static GameObject Group(Transform parent, string name, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size,
                                     string matKey, Vector3 euler = default, bool collide = false)
            => Shape(parent, name, _cube, pos, size, matKey, euler, collide);

        public static GameObject Cyl(Transform parent, string name, Vector3 pos, float radius,
                                     float height, string matKey, Vector3 euler = default, bool collide = false)
            => Shape(parent, name, _cyl, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f),
                     matKey, euler, collide);

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, float diameter,
                                        string matKey)
            => Shape(parent, name, _sphere, pos, Vector3.one * diameter, matKey, default, false);

        public static GameObject Quad(Transform parent, string name, Vector3 pos, Vector2 size,
                                      string matKey, Vector3 euler = default)
            => Shape(parent, name, _quad, pos, new Vector3(size.x, size.y, 1f), matKey, euler, false);

        private static GameObject Shape(Transform parent, string name, Mesh mesh, Vector3 pos,
                                        Vector3 size, string matKey, Vector3 euler, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(matKey);
            if (collide)
            {
                if (mesh == _cube) go.AddComponent<BoxCollider>();
                else if (mesh == _cyl) go.AddComponent<CapsuleCollider>();
                else go.AddComponent<BoxCollider>();
            }
            return go;
        }

        // =====================================================================
        //  Text
        // =====================================================================
        public enum TextRole { Sign, Title, Heading, Body, Caption, Mono }

        public static TextMeshPro Label(Transform parent, string name, Vector3 pos, Vector2 boxSize,
                                        string text, TextRole role, Color color,
                                        TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
                                        Vector3 euler = default)
        {
            // Creating the GameObject WITH the TMP type ensures its transform is a
            // RectTransform from the start (TMP requires one).
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);

            var tmp = go.GetComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.richText = true;
            tmp.raycastTarget = false;

            // Auto-size inside a metre-accurate box → legibility without guessing pt.
            // Min raised from 0.04 → 0.09 so small captions/bodies stay readable in VR
            // (0.04 let long copy shrink to an unreadable size on the wall boards).
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.09f;
            tmp.fontSizeMax = MaxSize(role);
            tmp.margin = new Vector4(0.02f, 0.01f, 0.02f, 0.01f);
            if (role == TextRole.Sign || role == TextRole.Title)
                tmp.fontStyle = FontStyles.Bold;

            // Centre anchors/pivot so sizeDelta is the real metre size and
            // localPosition centres the block.
            var rt = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = boxSize;
            rt.localPosition = pos;

            // A 3D TextMeshPro's readable face looks down its LOCAL -Z, so a label
            // authored "facing +Z" actually turns its mirror-reversed BACK to the
            // viewer. For upright signage (banners, wall signs, plaques, case
            // labels) we add a 180° yaw so the front faces the reader. Labels that
            // lie flat and are read from above (euler.x ≈ ±90°, e.g. the floor
            // medallion, desk signs, the Caesar-disk ring) are already correct and
            // must NOT be flipped. If a future test ever shows the opposite, change
            // the +180f below to +0f — this is the one place all C# text is built.
            float faceYaw = euler.y;
            if (Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) < 45f) faceYaw += 180f;
            rt.localEulerAngles = new Vector3(euler.x, faceYaw, euler.z);
            return tmp;
        }

        private static float MaxSize(TextRole role)
        {
            switch (role)
            {
                case TextRole.Sign: return 2.4f;
                case TextRole.Title: return 1.2f;
                case TextRole.Heading: return 0.85f;
                case TextRole.Body: return 0.58f;
                case TextRole.Caption: return 0.42f;
                case TextRole.Mono: return 0.6f;
                default: return 0.5f;
            }
        }

        // Convenience colours used across the museum.
        public static readonly Color Ink = new Color(0.10f, 0.10f, 0.11f);
        public static readonly Color Cream = new Color(0.93f, 0.90f, 0.82f);
        public static readonly Color BrassText = new Color(0.92f, 0.78f, 0.46f);
        public static readonly Color CyanText = new Color(0.55f, 0.92f, 1f);
        public static readonly Color WarmWhite = new Color(0.98f, 0.95f, 0.88f);

        // =====================================================================
        //  Lighting  (use sparingly — Quest budget; most glow is emissive)
        // =====================================================================
        public static Light Spot(Transform parent, string name, Vector3 pos, Vector3 aimEuler,
                                 Color color, float intensity, float range, float angle,
                                 bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = aimEuler;
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        public static Light Point(Transform parent, string name, Vector3 pos, Color color,
                                  float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        // =====================================================================
        //  Asset folder helper
        // =====================================================================
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
