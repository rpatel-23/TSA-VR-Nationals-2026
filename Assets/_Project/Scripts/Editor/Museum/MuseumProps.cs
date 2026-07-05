// -----------------------------------------------------------------------------
//  MuseumProps.cs   (Editor)
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  The museum's vocabulary of objects, composed from MuseumKit primitives. Every
//  display case, pedestal, plaque, column, bench, sign, banner, planter, kiosk and
//  artifact a visitor sees is stamped out by one of these builders. Each returns
//  the root of a small hierarchy placed in the parent's local space (metres),
//  facing +Z at yaw 0.
//
//  Two ideas keep the result dense yet performant on Quest:
//   * Plaque TITLES are always legible; long BODY copy fades in on approach via the
//     existing PlaqueController, so a room reads as full from afar and rewards a
//     closer look (and saves fill-rate when you are not reading).
//   * "Display lights" are emissive panels (free) rather than real lights; only a
//     few hero spotlights are real Lights, added by the builder.
// -----------------------------------------------------------------------------

using Decrypted.Visuals;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Decrypted.EditorTools
{
    public static class MuseumProps
    {
        // =====================================================================
        //  PLAQUES & PANELS
        // =====================================================================

        /// <summary>A freestanding/wall brass plaque: always-on title + proximity body.</summary>
        public static GameObject Plaque(Transform parent, Vector3 pos, float yaw,
                                        string title, string body, float width = 0.62f,
                                        bool alwaysBody = false)
        {
            var root = MuseumKit.Group(parent, "Plaque_" + Slug(title), pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);

            float h = 0.46f;
            MuseumKit.Box(root.transform, "Plate", new Vector3(0, 0, 0), new Vector3(width, h, 0.02f), "brassDark");
            MuseumKit.Box(root.transform, "Bevel", new Vector3(0, 0, -0.006f), new Vector3(width - 0.03f, h - 0.03f, 0.02f), "brass");

            // The plaque plate is a DARK brass ("brassDark") panel, so the title and
            // body use a warm WHITE for high contrast (dark ink read as illegible
            // "gray on yellowish" before). Body box widened to fit larger text.
            float ix = -width * 0.5f + 0.04f;
            var titleT = MuseumKit.Label(root.transform, "Title", new Vector3(0, h * 0.5f - 0.07f, 0.013f),
                new Vector2(width - 0.08f, 0.12f), title, MuseumKit.TextRole.Heading,
                MuseumKit.WarmWhite, TextAlignmentOptions.TopLeft);
            titleT.rectTransform.localPosition = new Vector3(ix + (width - 0.08f) * 0.5f, h * 0.5f - 0.06f, 0.013f);

            var bodyT = MuseumKit.Label(root.transform, "Body", new Vector3(0, -0.02f, 0.013f),
                new Vector2(width - 0.08f, h - 0.18f), body, MuseumKit.TextRole.Caption,
                MuseumKit.WarmWhite, TextAlignmentOptions.TopLeft);
            bodyT.rectTransform.localPosition = new Vector3(ix + (width - 0.08f) * 0.5f, -0.02f, 0.013f);

            WireBodyReveal(bodyT.gameObject, bodyT, body, alwaysBody);
            return root;
        }

        /// <summary>The full museum-label format: TITLE / DATE / DESCRIPTION / SIGNIFICANCE.</summary>
        public static GameObject LabelCard(Transform parent, Vector3 pos, float yaw, Exhibit ex,
                                           float width = 0.50f, float height = 0.40f)
        {
            var root = MuseumKit.Group(parent, "Label_" + ex.Id, pos);
            root.transform.localEulerAngles = new Vector3(12f, yaw, 0); // slight reading tilt

            // DARK card + WHITE text: in dim galleries (esp. WWII) the old cream card
            // with dark ink read as vague "gray text". White-on-slate stays crisp.
            MuseumKit.Box(root.transform, "Card", Vector3.zero, new Vector3(width, height, 0.012f), "marbleDark");
            MuseumKit.Box(root.transform, "Edge", new Vector3(0, 0, -0.004f), new Vector3(width + 0.012f, height + 0.012f, 0.008f), "brassDark");

            float pad = 0.03f;
            float innerW = width - pad * 2f;
            float top = height * 0.5f - pad;

            MuseumKit.Label(root.transform, "Title", new Vector3(0, top - 0.03f, 0.009f),
                new Vector2(innerW, 0.07f), ex.Title, MuseumKit.TextRole.Heading, MuseumKit.WarmWhite,
                TextAlignmentOptions.TopLeft);
            MuseumKit.Label(root.transform, "Date", new Vector3(0, top - 0.10f, 0.009f),
                new Vector2(innerW, 0.04f), "<b>DATE</b>   " + ex.Date, MuseumKit.TextRole.Caption,
                MuseumKit.BrassText, TextAlignmentOptions.TopLeft);
            var body = MuseumKit.Label(root.transform, "Body", new Vector3(0, top - 0.165f, 0.009f),
                new Vector2(innerW, height - 0.24f),
                ex.Description + "\n\n<b>SIGNIFICANCE</b>\n" + ex.Significance,
                MuseumKit.TextRole.Caption, MuseumKit.WarmWhite, TextAlignmentOptions.TopLeft);

            WireBodyReveal(body.gameObject, body, body.text, false);
            return root;
        }

        /// <summary>A large framed wall infographic panel with a headline, body and accent graphic.</summary>
        public static GameObject WallPanel(Transform parent, Vector3 pos, float yaw, Vector2 size,
                                           string headline, string body, string accentKey = "brass",
                                           string graphic = "panel")
        {
            var root = MuseumKit.Group(parent, "Panel_" + Slug(headline), pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);

            float w = size.x, h = size.y;
            MuseumKit.Box(root.transform, "Frame", new Vector3(0, 0, -0.02f), new Vector3(w + 0.10f, h + 0.10f, 0.04f), "woodDark");
            MuseumKit.Box(root.transform, "Mat", new Vector3(0, 0, 0f), new Vector3(w, h, 0.02f), "paperCream");
            MuseumKit.Box(root.transform, "AccentBar", new Vector3(0, h * 0.5f - 0.12f, 0.012f), new Vector3(w, 0.03f, 0.01f), accentKey);

            // A stylised graphic block (kept abstract — emissive accent reads as an "image").
            if (graphic == "map") MapGraphic(root.transform, new Vector3(0, 0.06f, 0.013f), new Vector2(w * 0.92f, h * 0.5f));
            else if (graphic != "none")
                MuseumKit.Box(root.transform, "Graphic", new Vector3(0, 0.10f, 0.012f),
                    new Vector3(w * 0.9f, h * 0.42f, 0.006f), "marbleDark");

            MuseumKit.Label(root.transform, "Headline", new Vector3(0, h * 0.5f - 0.07f, 0.014f),
                new Vector2(w - 0.1f, 0.13f), headline, MuseumKit.TextRole.Heading, MuseumKit.Ink,
                TextAlignmentOptions.Top);
            var bod = MuseumKit.Label(root.transform, "Body", new Vector3(0, -h * 0.32f, 0.014f),
                new Vector2(w - 0.14f, h * 0.34f), body, MuseumKit.TextRole.Body, MuseumKit.Ink,
                TextAlignmentOptions.Top);
            WireBodyReveal(bod.gameObject, bod, body, false);
            return root;
        }

        /// <summary>A framed portrait of a cryptology pioneer with an engraved nameplate + bio.</summary>
        public static GameObject Portrait(Transform parent, Vector3 pos, float yaw, Figure fig)
        {
            var root = MuseumKit.Group(parent, "Portrait_" + Slug(fig.Name), pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);

            float w = 0.62f, h = 0.82f;
            MuseumKit.Box(root.transform, "Frame", new Vector3(0, 0, -0.02f), new Vector3(w + 0.12f, h + 0.12f, 0.05f), "gold");
            MuseumKit.Box(root.transform, "FrameInner", new Vector3(0, 0, -0.01f), new Vector3(w + 0.04f, h + 0.04f, 0.04f), "woodDark");
            MuseumKit.Box(root.transform, "Canvas", Vector3.zero, new Vector3(w, h, 0.02f), "paperAged");

            // A small engraved monogram header sits at the TOP of the canvas, leaving
            // the body of the canvas free for the bio (see below).
            string initials = Initials(fig.Name);
            MuseumKit.Label(root.transform, "Monogram", new Vector3(0, 0.31f, 0.013f),
                new Vector2(w * 0.8f, 0.16f), initials, MuseumKit.TextRole.Sign,
                new Color(0.40f, 0.30f, 0.16f), TextAlignmentOptions.Center);

            // Nameplate
            MuseumKit.Box(root.transform, "Plate", new Vector3(0, -h * 0.5f + 0.08f, 0.014f), new Vector3(w * 0.9f, 0.14f, 0.012f), "brass");
            MuseumKit.Label(root.transform, "Name", new Vector3(0, -h * 0.5f + 0.115f, 0.022f),
                new Vector2(w * 0.86f, 0.06f), fig.Name, MuseumKit.TextRole.Caption, MuseumKit.Ink,
                TextAlignmentOptions.Center);
            MuseumKit.Label(root.transform, "Years", new Vector3(0, -h * 0.5f + 0.055f, 0.022f),
                new Vector2(w * 0.86f, 0.04f), fig.Years + "  ·  " + fig.Role, MuseumKit.TextRole.Caption,
                new Color(0.4f, 0.3f, 0.14f), TextAlignmentOptions.Center);

            // Bio sits ON the canvas (in front of it), between the monogram header and
            // the nameplate - dark ink on the light canvas. Previously it floated below
            // the frame, so it rendered on the wall behind/under the plaque.
            var bio = MuseumKit.Label(root.transform, "Bio", new Vector3(0, 0.0f, 0.022f),
                new Vector2(w * 0.9f, 0.44f), fig.Bio, MuseumKit.TextRole.Caption, MuseumKit.Ink,
                TextAlignmentOptions.Top);
            WireBodyReveal(bio.gameObject, bio, fig.Bio, true);
            return root;
        }

        // =====================================================================
        //  EXHIBIT FURNITURE
        // =====================================================================

        /// <summary>A glass vitrine on a plinth with an artifact, a top display light, and a label.</summary>
        public static GameObject DisplayCase(Transform parent, Vector3 pos, float yaw, Exhibit ex,
                                             string baseKey = "woodDark")
        {
            var root = MuseumKit.Group(parent, "Case_" + ex.Id, pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);

            float w = 0.7f, d = 0.7f, baseH = 0.95f, caseH = 0.7f;
            // Plinth
            MuseumKit.Box(root.transform, "Base", new Vector3(0, baseH * 0.5f, 0), new Vector3(w, baseH, d), baseKey, default, true);
            MuseumKit.Box(root.transform, "BaseTrim", new Vector3(0, baseH + 0.01f, 0), new Vector3(w + 0.04f, 0.04f, d + 0.04f), "brassDark");
            // Glass box
            float gy = baseH + 0.04f;
            MuseumKit.Box(root.transform, "Glass", new Vector3(0, gy + caseH * 0.5f, 0), new Vector3(w - 0.06f, caseH, d - 0.06f), "glass");
            // Corner posts
            float px = (w - 0.06f) * 0.5f, pz = (d - 0.06f) * 0.5f;
            foreach (var s in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                MuseumKit.Box(root.transform, "Post", new Vector3(px * s.x, gy + caseH * 0.5f, pz * s.y), new Vector3(0.025f, caseH, 0.025f), "brass");
            // Lid + display light (emissive disc under lid)
            MuseumKit.Box(root.transform, "Lid", new Vector3(0, gy + caseH + 0.015f, 0), new Vector3(w, 0.05f, d), "brassDark");
            MuseumKit.Box(root.transform, "Light", new Vector3(0, gy + caseH - 0.01f, 0), new Vector3(w * 0.5f, 0.012f, d * 0.5f), "glowWarm");

            // Artifact floating on the plinth
            Artifact(root.transform, new Vector3(0, gy + 0.12f, 0), ex.Artifact, 0.26f);

            // Front label plate
            LabelCard(root.transform, new Vector3(0, gy + 0.10f, d * 0.5f - 0.01f), 0f, ex, 0.5f, 0.32f);
            return root;
        }

        /// <summary>An open plinth with a spotlit artifact on a (optionally spinning) turntable.</summary>
        public static GameObject Pedestal(Transform parent, Vector3 pos, float yaw, Exhibit ex,
                                          bool spin = true, string stoneKey = "marbleLight",
                                          float height = 1.0f, float artScale = 0.34f)
        {
            var root = MuseumKit.Group(parent, "Pedestal_" + ex.Id, pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);

            float topW = 0.5f;
            MuseumKit.Box(root.transform, "Foot", new Vector3(0, 0.04f, 0), new Vector3(topW + 0.16f, 0.08f, topW + 0.16f), "marbleDark", default, true);
            MuseumKit.Box(root.transform, "Shaft", new Vector3(0, height * 0.5f, 0), new Vector3(topW, height, topW), stoneKey, default, true);
            MuseumKit.Box(root.transform, "Cap", new Vector3(0, height + 0.03f, 0), new Vector3(topW + 0.12f, 0.06f, topW + 0.12f), "marbleDark");

            // Turntable + artifact
            var turn = MuseumKit.Group(root.transform, "Turntable", new Vector3(0, height + 0.06f, 0));
            MuseumKit.Cyl(turn.transform, "Disc", new Vector3(0, 0.01f, 0), 0.16f, 0.02f, "brass");
            Artifact(turn.transform, new Vector3(0, 0.06f, 0), ex.Artifact, artScale);
            if (spin) turn.AddComponent<MuseumSpin>().DegreesPerSecond = 9f;

            // Nameplate on a small angled stand at the front edge
            Plaque(root.transform, new Vector3(0, height * 0.62f, topW * 0.5f + 0.02f), 0f, ex.Title, ex.Description + "\n\n<b>SIGNIFICANCE</b>  " + ex.Significance, 0.52f);
            return root;
        }

        /// <summary>A low angled table case for manuscripts, maps, letters.</summary>
        public static GameObject TabletCase(Transform parent, Vector3 pos, float yaw, Exhibit ex)
        {
            var root = MuseumKit.Group(parent, "Tablet_" + ex.Id, pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);

            float w = 0.9f, d = 0.6f, h = 0.92f;
            MuseumKit.Box(root.transform, "Legs", new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), "woodDark", default, true);
            MuseumKit.Box(root.transform, "Top", new Vector3(0, h + 0.02f, 0), new Vector3(w + 0.06f, 0.05f, d + 0.06f), "wood");
            // Angled glass + content
            var slope = MuseumKit.Group(root.transform, "Display", new Vector3(0, h + 0.06f, 0));
            slope.transform.localEulerAngles = new Vector3(-22f, 0, 0);
            MuseumKit.Box(slope.transform, "Felt", new Vector3(0, 0, -0.01f), new Vector3(w * 0.92f, d * 0.9f, 0.01f), "feltGreen");
            Artifact(slope.transform, new Vector3(0, 0.02f, 0.01f), ex.Artifact, 0.3f);
            MuseumKit.Box(slope.transform, "Glass", new Vector3(0, 0, 0.06f), new Vector3(w * 0.94f, d * 0.92f, 0.01f), "glass");
            // Label below front edge
            LabelCard(root.transform, new Vector3(0, h * 0.5f, d * 0.5f + 0.02f), 0f, ex, 0.52f, 0.3f);
            return root;
        }

        /// <summary>A large object mounted on the wall (machine, rack, relief) + plaque.</summary>
        public static GameObject WallExhibit(Transform parent, Vector3 pos, float yaw, Exhibit ex)
        {
            var root = MuseumKit.Group(parent, "WallEx_" + ex.Id, pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Backboard", new Vector3(0, 0, -0.03f), new Vector3(1.1f, 1.1f, 0.04f), "marbleDark");
            Artifact(root.transform, new Vector3(0, 0, 0.12f), ex.Artifact, 0.7f);
            MuseumKit.Box(root.transform, "Light", new Vector3(0, 0.62f, 0.14f), new Vector3(0.5f, 0.03f, 0.06f), "glowWarm");
            Plaque(root.transform, new Vector3(0, -0.72f, 0.06f), 0f, ex.Title, ex.Description + "\n\n<b>SIGNIFICANCE</b>  " + ex.Significance, 0.78f);
            return root;
        }

        /// <summary>Dispatch any exhibit to the right furniture by its CaseKind.</summary>
        public static GameObject PlaceExhibit(Transform parent, Vector3 pos, float yaw, Exhibit ex)
        {
            switch (ex.Kind)
            {
                case CaseKind.Vitrine: return DisplayCase(parent, pos, yaw, ex);
                case CaseKind.Pedestal: return Pedestal(parent, pos, yaw, ex);
                case CaseKind.Tablet: return TabletCase(parent, pos, yaw, ex);
                case CaseKind.Relief: return WallExhibit(parent, pos, yaw, ex);
                case CaseKind.WallPanel:
                    return WallPanel(parent, pos, yaw, new Vector2(1.3f, 0.95f), ex.Title,
                        ex.Description + "\n\n<b>SIGNIFICANCE</b>  " + ex.Significance, "brass", ex.Artifact);
                default: return DisplayCase(parent, pos, yaw, ex);
            }
        }

        // =====================================================================
        //  ARTIFACTS  (small recognisable shapes from primitives)
        // =====================================================================
        public static GameObject Artifact(Transform parent, Vector3 pos, string hint, float scale)
        {
            var root = MuseumKit.Group(parent, "Artifact_" + (hint ?? "x"), pos);
            float s = scale;
            switch (hint)
            {
                case "rod":
                    MuseumKit.Cyl(root.transform, "Rod", Vector3.zero, 0.04f * s / 0.3f, s * 1.6f, "wood", new Vector3(0, 0, 90));
                    MuseumKit.Cyl(root.transform, "Scroll", new Vector3(0, 0.05f * s, 0), 0.06f * s / 0.3f, s * 0.9f, "paperAged", new Vector3(0, 0, 90));
                    break;
                case "disk":
                    MuseumKit.Cyl(root.transform, "Outer", Vector3.zero, s * 0.6f, s * 0.16f, "brass");
                    MuseumKit.Cyl(root.transform, "Inner", new Vector3(0, s * 0.1f, 0), s * 0.4f, s * 0.14f, "copper");
                    break;
                case "scroll": case "letter": case "manuscript":
                    MuseumKit.Cyl(root.transform, "Scroll", Vector3.zero, s * 0.18f, s * 1.2f, "paperAged", new Vector3(0, 0, 90));
                    MuseumKit.Cyl(root.transform, "RodA", new Vector3(-s * 0.62f, 0, 0), s * 0.06f, s * 1.3f, "woodDark", new Vector3(0, 0, 90));
                    MuseumKit.Cyl(root.transform, "RodB", new Vector3(s * 0.62f, 0, 0), s * 0.06f, s * 1.3f, "woodDark", new Vector3(0, 0, 90));
                    break;
                case "tablet": case "grid":
                    MuseumKit.Box(root.transform, "Tablet", Vector3.zero, new Vector3(s * 1.1f, s * 1.4f, s * 0.18f), "sandstone", new Vector3(8, 0, 0));
                    break;
                case "book": case "codex": case "ledger":
                    MuseumKit.Box(root.transform, "Cover", Vector3.zero, new Vector3(s * 0.9f, s * 0.22f, s * 1.2f), "velvetRed");
                    MuseumKit.Box(root.transform, "Pages", new Vector3(0, s * 0.02f, 0), new Vector3(s * 0.82f, s * 0.2f, s * 1.12f), "paperCream");
                    break;
                case "pad":
                    MuseumKit.Box(root.transform, "Pad", Vector3.zero, new Vector3(s * 0.9f, s * 0.1f, s * 1.2f), "paperCream");
                    break;
                case "rotor":
                    MuseumKit.Cyl(root.transform, "Body", Vector3.zero, s * 0.5f, s * 0.9f, "brass", new Vector3(0, 0, 90));
                    MuseumKit.Cyl(root.transform, "RingA", new Vector3(s * 0.42f, 0, 0), s * 0.55f, s * 0.1f, "copper", new Vector3(0, 0, 90));
                    MuseumKit.Cyl(root.transform, "RingB", new Vector3(-s * 0.42f, 0, 0), s * 0.55f, s * 0.1f, "copper", new Vector3(0, 0, 90));
                    break;
                case "drums": case "rack":
                    for (int i = 0; i < 3; i++)
                        MuseumKit.Cyl(root.transform, "Drum" + i, new Vector3((i - 1) * s * 0.45f, 0, 0), s * 0.32f, s * 1.3f, "ironDark");
                    MuseumKit.Box(root.transform, "Frame", new Vector3(0, -s * 0.7f, 0), new Vector3(s * 1.7f, s * 0.12f, s * 0.8f), "steel");
                    break;
                case "handset": case "machine":
                    MuseumKit.Box(root.transform, "Body", Vector3.zero, new Vector3(s, s * 0.5f, s * 0.8f), "ironDark");
                    MuseumKit.Cyl(root.transform, "Knob1", new Vector3(-s * 0.25f, s * 0.32f, 0), s * 0.1f, s * 0.12f, "brass");
                    MuseumKit.Cyl(root.transform, "Knob2", new Vector3(s * 0.25f, s * 0.32f, 0), s * 0.1f, s * 0.12f, "brass");
                    break;
                case "telegram":
                    MuseumKit.Box(root.transform, "Paper", Vector3.zero, new Vector3(s * 1.0f, s * 0.04f, s * 1.3f), "paperAged", new Vector3(6, 0, 0));
                    break;
                case "chip":
                    MuseumKit.Box(root.transform, "Die", Vector3.zero, new Vector3(s * 0.9f, s * 0.16f, s * 0.9f), "blackMatte");
                    for (int i = 0; i < 5; i++) {
                        MuseumKit.Box(root.transform, "PinL" + i, new Vector3(-s * 0.5f, 0, (i - 2) * s * 0.16f), new Vector3(s * 0.2f, s * 0.04f, s * 0.06f), "gold");
                        MuseumKit.Box(root.transform, "PinR" + i, new Vector3(s * 0.5f, 0, (i - 2) * s * 0.16f), new Vector3(s * 0.2f, s * 0.04f, s * 0.06f), "gold");
                    }
                    break;
                case "padlock":
                    MuseumKit.Box(root.transform, "Body", new Vector3(0, -s * 0.1f, 0), new Vector3(s * 0.7f, s * 0.7f, s * 0.35f), "steel");
                    MuseumKit.Cyl(root.transform, "ShackleL", new Vector3(-s * 0.22f, s * 0.35f, 0), s * 0.06f, s * 0.5f, "chrome");
                    MuseumKit.Cyl(root.transform, "ShackleR", new Vector3(s * 0.22f, s * 0.35f, 0), s * 0.06f, s * 0.5f, "chrome");
                    MuseumKit.Cyl(root.transform, "ShackleTop", new Vector3(0, s * 0.55f, 0), s * 0.06f, s * 0.46f, "chrome", new Vector3(0, 0, 90));
                    MuseumKit.Cyl(root.transform, "Keyhole", new Vector3(0, -s * 0.12f, s * 0.18f), s * 0.08f, s * 0.05f, "blackMatte", new Vector3(90, 0, 0));
                    break;
                case "keys":
                    MuseumKit.Cyl(root.transform, "Ring", new Vector3(0, s * 0.35f, 0), s * 0.22f, s * 0.06f, "gold", new Vector3(90, 0, 0));
                    MuseumKit.Box(root.transform, "Shaft", new Vector3(0, -s * 0.1f, 0), new Vector3(s * 0.1f, s * 0.7f, s * 0.06f), "gold");
                    MuseumKit.Box(root.transform, "Bit", new Vector3(s * 0.12f, -s * 0.38f, 0), new Vector3(s * 0.16f, s * 0.18f, s * 0.06f), "gold");
                    break;
                case "prism":
                    MuseumKit.Box(root.transform, "Prism", Vector3.zero, new Vector3(s * 0.6f, s * 0.6f, s * 0.6f), "glowCyan", new Vector3(20, 30, 15));
                    break;
                case "photon":
                    MuseumKit.Sphere(root.transform, "Core", Vector3.zero, s * 0.5f, "glowCyan");
                    MuseumKit.Cyl(root.transform, "Halo", Vector3.zero, s * 0.5f, s * 0.02f, "glowBlue", new Vector3(70, 0, 0));
                    break;
                case "lattice":
                    for (int x = 0; x < 3; x++)
                        for (int y = 0; y < 3; y++)
                            for (int z = 0; z < 3; z++)
                                MuseumKit.Sphere(root.transform, $"N{x}{y}{z}",
                                    new Vector3((x - 1) * s * 0.4f, (y - 1) * s * 0.4f, (z - 1) * s * 0.4f), s * 0.12f, "glowBlue");
                    break;
                case "map":
                    MuseumKit.Box(root.transform, "Sheet", Vector3.zero, new Vector3(s * 1.4f, s * 0.03f, s * 1.0f), "paperAged");
                    break;
                default:
                    MuseumKit.Box(root.transform, "Object", Vector3.zero, new Vector3(s * 0.6f, s * 0.6f, s * 0.6f), "bronze", new Vector3(15, 25, 10));
                    break;
            }
            return root;
        }

        // =====================================================================
        //  ARCHITECTURE ACCENTS
        // =====================================================================
        public static GameObject Column(Transform parent, Vector3 basePos, float height, float radius = 0.34f,
                                        string key = "marbleLight")
        {
            var root = MuseumKit.Group(parent, "Column", basePos);
            MuseumKit.Box(root.transform, "Base", new Vector3(0, 0.12f, 0), new Vector3(radius * 2.6f, 0.24f, radius * 2.6f), "marbleDark", default, true);
            MuseumKit.Box(root.transform, "Plinth", new Vector3(0, 0.3f, 0), new Vector3(radius * 2.2f, 0.12f, radius * 2.2f), key);
            MuseumKit.Cyl(root.transform, "Shaft", new Vector3(0, height * 0.5f + 0.3f, 0), radius, height - 0.6f, key, default, true);
            // subtle fluting suggestion
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                MuseumKit.Box(root.transform, "Flute" + i, new Vector3(Mathf.Cos(a) * radius, height * 0.5f + 0.3f, Mathf.Sin(a) * radius),
                    new Vector3(0.03f, height - 0.7f, 0.03f), "marbleDark");
            }
            MuseumKit.Box(root.transform, "Capital", new Vector3(0, height + 0.06f, 0), new Vector3(radius * 2.6f, 0.18f, radius * 2.6f), key);
            MuseumKit.Box(root.transform, "Abacus", new Vector3(0, height + 0.17f, 0), new Vector3(radius * 2.9f, 0.08f, radius * 2.9f), "marbleDark");
            return root;
        }

        public static GameObject Archway(Transform parent, Vector3 pos, float yaw, float width, float height,
                                         string key = "marbleLight")
        {
            var root = MuseumKit.Group(parent, "Archway", pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            float pierW = 0.4f;
            MuseumKit.Box(root.transform, "PierL", new Vector3(-width * 0.5f, height * 0.5f, 0), new Vector3(pierW, height, pierW), key, default, true);
            MuseumKit.Box(root.transform, "PierR", new Vector3(width * 0.5f, height * 0.5f, 0), new Vector3(pierW, height, pierW), key, default, true);
            // shallow arch from segments
            int seg = 9;
            float r = width * 0.5f;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(0, Mathf.PI, i / (float)seg);
                float x = -Mathf.Cos(a) * r;
                float y = height + Mathf.Sin(a) * 0.5f;
                MuseumKit.Box(root.transform, "Arch" + i, new Vector3(x, y, 0), new Vector3(width / seg + 0.05f, 0.22f, pierW), key,
                    new Vector3(0, 0, Mathf.Rad2Deg * (a - Mathf.PI * 0.5f)));
            }
            MuseumKit.Box(root.transform, "Keystone", new Vector3(0, height + 0.5f, 0), new Vector3(0.22f, 0.3f, pierW + 0.04f), "brass");
            return root;
        }

        public static GameObject Bench(Transform parent, Vector3 pos, float yaw, string key = "marbleLight")
        {
            var root = MuseumKit.Group(parent, "Bench", pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Seat", new Vector3(0, 0.46f, 0), new Vector3(1.5f, 0.1f, 0.5f), key, default, true);
            MuseumKit.Box(root.transform, "Cushion", new Vector3(0, 0.53f, 0), new Vector3(1.42f, 0.06f, 0.44f), "velvetRed");
            MuseumKit.Box(root.transform, "LegL", new Vector3(-0.62f, 0.22f, 0), new Vector3(0.12f, 0.42f, 0.42f), "marbleDark");
            MuseumKit.Box(root.transform, "LegR", new Vector3(0.62f, 0.22f, 0), new Vector3(0.12f, 0.42f, 0.42f), "marbleDark");
            return root;
        }

        // =====================================================================
        //  WAYFINDING & FACILITIES
        // =====================================================================
        public static GameObject HangingSign(Transform parent, Vector3 pos, string text, string key = "brassDark")
        {
            var root = MuseumKit.Group(parent, "HangSign_" + Slug(text), pos);
            MuseumKit.Box(root.transform, "RodL", new Vector3(-0.7f, 0.5f, 0), new Vector3(0.03f, 1.0f, 0.03f), "brass");
            MuseumKit.Box(root.transform, "RodR", new Vector3(0.7f, 0.5f, 0), new Vector3(0.03f, 1.0f, 0.03f), "brass");
            MuseumKit.Box(root.transform, "Board", Vector3.zero, new Vector3(1.8f, 0.5f, 0.06f), key);
            MuseumKit.Label(root.transform, "TextFront", new Vector3(0, 0, 0.04f), new Vector2(1.7f, 0.42f), text,
                MuseumKit.TextRole.Sign, MuseumKit.BrassText, TextAlignmentOptions.Center);
            MuseumKit.Label(root.transform, "TextBack", new Vector3(0, 0, -0.04f), new Vector2(1.7f, 0.42f), text,
                MuseumKit.TextRole.Sign, MuseumKit.BrassText, TextAlignmentOptions.Center, new Vector3(0, 180, 0));
            return root;
        }

        public static GameObject WallSign(Transform parent, Vector3 pos, float yaw, string name, string subtitle, string accent = "brass")
        {
            var root = MuseumKit.Group(parent, "WallSign_" + Slug(name), pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Plate", new Vector3(0, 0, -0.01f), new Vector3(2.6f, 0.7f, 0.05f), "marbleDark");
            MuseumKit.Box(root.transform, "Accent", new Vector3(0, -0.28f, 0.02f), new Vector3(2.4f, 0.03f, 0.01f), accent);
            MuseumKit.Label(root.transform, "Name", new Vector3(0, 0.08f, 0.03f), new Vector2(2.4f, 0.34f), name,
                MuseumKit.TextRole.Sign, MuseumKit.WarmWhite, TextAlignmentOptions.Center);
            MuseumKit.Label(root.transform, "Sub", new Vector3(0, -0.18f, 0.03f), new Vector2(2.4f, 0.12f), subtitle,
                MuseumKit.TextRole.Caption, MuseumKit.BrassText, TextAlignmentOptions.Center);
            return root;
        }

        public static GameObject DirectorySign(Transform parent, Vector3 pos, float yaw, string heading, string[] lines)
        {
            var root = MuseumKit.Group(parent, "Directory", pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Post", new Vector3(0, 0.8f, 0), new Vector3(0.1f, 1.6f, 0.1f), "ironDark", default, true);
            MuseumKit.Box(root.transform, "Board", new Vector3(0, 1.5f, 0.04f), new Vector3(1.2f, 1.5f, 0.06f), "marbleDark");
            MuseumKit.Box(root.transform, "Header", new Vector3(0, 2.13f, 0.06f), new Vector3(1.2f, 0.26f, 0.04f), "brass");
            MuseumKit.Label(root.transform, "Heading", new Vector3(0, 2.13f, 0.09f), new Vector2(1.12f, 0.2f), heading,
                MuseumKit.TextRole.Heading, MuseumKit.Ink, TextAlignmentOptions.Center);
            // Sit the list in the MIDDLE of the black board (vertical-centre), clear of
            // the gold header band at the top - otherwise the first line lands on the gold.
            MuseumKit.Label(root.transform, "Lines", new Vector3(0, 1.30f, 0.09f), new Vector2(1.08f, 1.00f),
                string.Join("\n", lines), MuseumKit.TextRole.Body, MuseumKit.WarmWhite, TextAlignmentOptions.Left);
            return root;
        }

        public static GameObject ExitSign(Transform parent, Vector3 pos, float yaw, string text = "EXIT")
        {
            var root = MuseumKit.Group(parent, "ExitSign", pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Box", Vector3.zero, new Vector3(0.7f, 0.26f, 0.05f), "glowGreen");
            MuseumKit.Label(root.transform, "Txt", new Vector3(0, 0, 0.04f), new Vector2(0.64f, 0.2f), text,
                MuseumKit.TextRole.Heading, Color.white, TextAlignmentOptions.Center);
            return root;
        }

        public static GameObject Kiosk(Transform parent, Vector3 pos, float yaw, string headline, string body)
        {
            var root = MuseumKit.Group(parent, "Kiosk_" + Slug(headline), pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Base", new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1.0f, 0.4f), "blackMatte", default, true);
            MuseumKit.Box(root.transform, "Pedestal", new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.08f, 0.5f), "steel");
            var screenHost = MuseumKit.Group(root.transform, "ScreenHost", new Vector3(0, 1.18f, 0.12f));
            screenHost.transform.localEulerAngles = new Vector3(22f, 0, 0);
            var screen = MuseumKit.Box(screenHost.transform, "Screen", Vector3.zero, new Vector3(0.6f, 0.42f, 0.03f), "screen");
            screen.AddComponent<MuseumGlowPulse>();
            MuseumKit.Label(screenHost.transform, "Head", new Vector3(0, 0.13f, 0.02f), new Vector2(0.54f, 0.1f), headline,
                MuseumKit.TextRole.Caption, MuseumKit.CyanText, TextAlignmentOptions.Top);
            MuseumKit.Label(screenHost.transform, "Body", new Vector3(0, -0.04f, 0.02f), new Vector2(0.54f, 0.26f), body,
                MuseumKit.TextRole.Caption, Color.white, TextAlignmentOptions.Top);
            MuseumKit.Label(root.transform, "Tag", new Vector3(0, 0.85f, 0.21f), new Vector2(0.55f, 0.06f), "◄ INTERACTIVE GUIDE ►",
                MuseumKit.TextRole.Caption, MuseumKit.CyanText, TextAlignmentOptions.Center);
            return root;
        }

        public static GameObject InfoDesk(Transform parent, Vector3 pos, float yaw)
        {
            var root = MuseumKit.Group(parent, "InfoDesk", pos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Counter", new Vector3(0, 0.5f, 0), new Vector3(2.4f, 1.0f, 0.7f), "wood", default, true);
            MuseumKit.Box(root.transform, "Top", new Vector3(0, 1.02f, 0), new Vector3(2.6f, 0.06f, 0.9f), "marbleLight");
            MuseumKit.Box(root.transform, "Front", new Vector3(0, 0.5f, 0.36f), new Vector3(2.42f, 0.9f, 0.04f), "brassDark");
            MuseumKit.Label(root.transform, "Sign", new Vector3(0, 1.05f, 0.3f), new Vector2(1.4f, 0.22f), "INFORMATION",
                MuseumKit.TextRole.Heading, MuseumKit.BrassText, TextAlignmentOptions.Center, new Vector3(90, 0, 0));
            BrochureStand(root.transform, new Vector3(0.9f, 1.05f, 0));
            return root;
        }

        public static GameObject BrochureStand(Transform parent, Vector3 pos)
        {
            var root = MuseumKit.Group(parent, "Brochures", pos);
            MuseumKit.Box(root.transform, "Tray", Vector3.zero, new Vector3(0.3f, 0.04f, 0.4f), "ironDark");
            MuseumKit.Box(root.transform, "Stack", new Vector3(0, 0.05f, 0), new Vector3(0.24f, 0.08f, 0.32f), "paperCream", new Vector3(-14, 0, 0));
            return root;
        }

        public static GameObject TrashBin(Transform parent, Vector3 pos)
        {
            var root = MuseumKit.Group(parent, "Bin", pos);
            MuseumKit.Cyl(root.transform, "Body", new Vector3(0, 0.35f, 0), 0.18f, 0.7f, "steel", default, true);
            MuseumKit.Cyl(root.transform, "Rim", new Vector3(0, 0.7f, 0), 0.2f, 0.05f, "ironDark");
            return root;
        }

        public static GameObject Planter(Transform parent, Vector3 pos, float scale = 1f)
        {
            var root = MuseumKit.Group(parent, "Planter", pos);
            MuseumKit.Cyl(root.transform, "Pot", new Vector3(0, 0.25f * scale, 0), 0.28f * scale, 0.5f * scale, "marbleDark", default, true);
            MuseumKit.Cyl(root.transform, "Soil", new Vector3(0, 0.5f * scale, 0), 0.24f * scale, 0.04f * scale, "woodDark");
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                MuseumKit.Sphere(root.transform, "Leaf" + i, new Vector3(Mathf.Cos(a) * 0.18f * scale, (0.75f + (i % 2) * 0.18f) * scale, Mathf.Sin(a) * 0.18f * scale), 0.4f * scale, "leaf");
            }
            MuseumKit.Sphere(root.transform, "Crown", new Vector3(0, 0.95f * scale, 0), 0.46f * scale, "leaf");
            return root;
        }

        /// <summary>A hanging chandelier (chain, gilt tiers, candle arms, glowing flames).</summary>
        public static GameObject Chandelier(Transform parent, Vector3 topPos, string glowKey)
        {
            var root = MuseumKit.Group(parent, "Chandelier", topPos);
            MuseumKit.Box(root.transform, "Chain", new Vector3(0, -0.35f, 0), new Vector3(0.04f, 0.7f, 0.04f), "brassDark");
            MuseumKit.Cyl(root.transform, "Crown", new Vector3(0, -0.7f, 0), 0.5f, 0.06f, "gold");
            MuseumKit.Cyl(root.transform, "Tier", new Vector3(0, -0.95f, 0), 0.34f, 0.05f, "gold");
            const int arms = 8;
            for (int i = 0; i < arms; i++)
            {
                float a = i / (float)arms * 360f, ar = a * Mathf.Deg2Rad;
                float cx = Mathf.Cos(ar), cz = Mathf.Sin(ar);
                MuseumKit.Box(root.transform, "Arm" + i, new Vector3(cx * 0.42f, -0.72f, cz * 0.42f), new Vector3(0.04f, 0.04f, 0.34f), "gold", new Vector3(0, -a, 0));
                MuseumKit.Cyl(root.transform, "Candle" + i, new Vector3(cx * 0.62f, -0.62f, cz * 0.62f), 0.03f, 0.16f, "whiteMatte");
                MuseumKit.Sphere(root.transform, "Flame" + i, new Vector3(cx * 0.62f, -0.5f, cz * 0.62f), 0.1f, glowKey);
            }
            MuseumKit.Sphere(root.transform, "CoreGlow", new Vector3(0, -0.85f, 0), 0.34f, glowKey);
            return root;
        }

        public static GameObject Banner(Transform parent, Vector3 topPos, Vector2 size, string text, string key = "velvetRed")
        {
            var root = MuseumKit.Group(parent, "Banner_" + Slug(text), topPos);
            MuseumKit.Box(root.transform, "Rod", new Vector3(0, 0, 0), new Vector3(size.x + 0.2f, 0.06f, 0.06f), "brass");
            MuseumKit.Box(root.transform, "Cloth", new Vector3(0, -size.y * 0.5f - 0.05f, 0.02f), new Vector3(size.x, size.y, 0.02f), key);
            MuseumKit.Box(root.transform, "Trim", new Vector3(0, -size.y - 0.05f, 0.02f), new Vector3(size.x, 0.06f, 0.03f), "gold");
            MuseumKit.Label(root.transform, "Text", new Vector3(0, -size.y * 0.5f - 0.05f, 0.04f), new Vector2(size.x - 0.1f, size.y - 0.2f),
                text, MuseumKit.TextRole.Title, MuseumKit.BrassText, TextAlignmentOptions.Center);
            return root;
        }

        public static GameObject Stanchion(Transform parent, Vector3 pos)
        {
            var root = MuseumKit.Group(parent, "Stanchion", pos);
            MuseumKit.Cyl(root.transform, "Base", new Vector3(0, 0.02f, 0), 0.16f, 0.04f, "ironDark", default, true);
            MuseumKit.Cyl(root.transform, "Post", new Vector3(0, 0.5f, 0), 0.03f, 0.95f, "brass");
            MuseumKit.Sphere(root.transform, "Cap", new Vector3(0, 0.98f, 0), 0.1f, "gold");
            return root;
        }

        /// <summary>Posts + velvet rope around a set of local points (closed loop).</summary>
        public static GameObject RopeBarrier(Transform parent, string name, Vector3[] points)
        {
            var root = MuseumKit.Group(parent, "Barrier_" + name, Vector3.zero);
            for (int i = 0; i < points.Length; i++)
            {
                Stanchion(root.transform, points[i]);
                Vector3 a = points[i] + Vector3.up * 0.82f;
                Vector3 b = points[(i + 1) % points.Length] + Vector3.up * 0.82f;
                Vector3 mid = (a + b) * 0.5f - Vector3.up * 0.08f; // sag
                float len = Vector3.Distance(a, b);
                var rope = MuseumKit.Cyl(root.transform, "Rope" + i, mid, 0.02f, len, "velvetRed");
                rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            }
            return root;
        }

        public static GameObject SecurityCamera(Transform parent, Vector3 pos, float yaw)
        {
            var root = MuseumKit.Group(parent, "Camera", pos);
            root.transform.localEulerAngles = new Vector3(20f, yaw, 0);
            MuseumKit.Box(root.transform, "Arm", new Vector3(0, 0.1f, -0.1f), new Vector3(0.05f, 0.05f, 0.2f), "ironDark");
            MuseumKit.Box(root.transform, "Body", Vector3.zero, new Vector3(0.12f, 0.1f, 0.22f), "whiteMatte");
            MuseumKit.Cyl(root.transform, "Lens", new Vector3(0, 0, 0.12f), 0.04f, 0.04f, "blackMatte", new Vector3(90, 0, 0));
            MuseumKit.Sphere(root.transform, "LED", new Vector3(0.04f, 0.04f, 0.1f), 0.02f, "glowRed");
            return root;
        }

        public static GameObject DonationBox(Transform parent, Vector3 pos)
        {
            var root = MuseumKit.Group(parent, "Donation", pos);
            MuseumKit.Box(root.transform, "Post", new Vector3(0, 0.45f, 0), new Vector3(0.1f, 0.9f, 0.1f), "ironDark", default, true);
            MuseumKit.Box(root.transform, "Box", new Vector3(0, 1.0f, 0), new Vector3(0.3f, 0.3f, 0.3f), "glass");
            MuseumKit.Box(root.transform, "Coins", new Vector3(0, 0.92f, 0), new Vector3(0.2f, 0.08f, 0.2f), "gold");
            MuseumKit.Label(root.transform, "Sign", new Vector3(0, 1.22f, 0), new Vector2(0.4f, 0.08f), "SUPPORT THE MUSEUM",
                MuseumKit.TextRole.Caption, MuseumKit.BrassText, TextAlignmentOptions.Center);
            return root;
        }

        // =====================================================================
        //  DATA-VISUAL PANELS
        // =====================================================================
        public static GameObject TimelineStrip(Transform parent, Vector3 centerPos, float yaw, float length,
                                               TimelineNode[] nodes, string title)
        {
            var root = MuseumKit.Group(parent, "Timeline", centerPos);
            root.transform.localEulerAngles = new Vector3(0, yaw, 0);
            MuseumKit.Box(root.transform, "Rail", Vector3.zero, new Vector3(length, 0.04f, 0.03f), "brass");
            MuseumKit.Label(root.transform, "Title", new Vector3(0, 0.55f, 0.02f), new Vector2(length * 0.6f, 0.16f), title,
                MuseumKit.TextRole.Heading, MuseumKit.BrassText, TextAlignmentOptions.Center);
            int n = nodes.Length;
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(-length * 0.5f + 0.3f, length * 0.5f - 0.3f, n == 1 ? 0.5f : i / (float)(n - 1));
                bool up = (i % 2 == 0);
                float sy = up ? 0.24f : -0.24f;
                MuseumKit.Box(root.transform, "Tick" + i, new Vector3(x, sy * 0.5f, 0.01f), new Vector3(0.02f, Mathf.Abs(sy), 0.02f), "brassDark");
                MuseumKit.Sphere(root.transform, "Dot" + i, new Vector3(x, 0, 0.02f), 0.06f, "glowGold");
                MuseumKit.Label(root.transform, "Year" + i, new Vector3(x, sy + (up ? 0.1f : -0.1f), 0.02f), new Vector2(0.5f, 0.07f),
                    "<b>" + nodes[i].Year + "</b>", MuseumKit.TextRole.Caption, MuseumKit.WarmWhite, TextAlignmentOptions.Center);
                MuseumKit.Label(root.transform, "Cap" + i, new Vector3(x, sy + (up ? 0.32f : -0.32f), 0.02f), new Vector2(0.62f, 0.24f),
                    nodes[i].Caption, MuseumKit.TextRole.Caption, MuseumKit.Cream, TextAlignmentOptions.Center);
            }
            return root;
        }

        private static void MapGraphic(Transform parent, Vector3 pos, Vector2 size)
        {
            var root = MuseumKit.Group(parent, "Map", pos);
            MuseumKit.Box(root.transform, "Sea", Vector3.zero, new Vector3(size.x, size.y, 0.004f), "carpetBlue");
            // abstract land masses
            var rnd = new System.Random(7);
            for (int i = 0; i < 6; i++)
            {
                float lx = (float)(rnd.NextDouble() - 0.5) * size.x * 0.8f;
                float ly = (float)(rnd.NextDouble() - 0.5) * size.y * 0.7f;
                float lw = size.x * (0.12f + (float)rnd.NextDouble() * 0.16f);
                float lh = size.y * (0.18f + (float)rnd.NextDouble() * 0.22f);
                MuseumKit.Box(root.transform, "Land" + i, new Vector3(lx, ly, 0.003f), new Vector3(lw, lh, 0.004f), "sandstone");
            }
            // pins where cryptography turned
            foreach (var p in new[] { new Vector2(-0.18f, 0.06f), new Vector2(-0.05f, 0.12f), new Vector2(0.05f, 0.02f), new Vector2(0.2f, 0.1f), new Vector2(-0.25f, -0.1f) })
                MuseumKit.Sphere(root.transform, "Pin", new Vector3(p.x * size.x, p.y * size.y, 0.012f), 0.05f, "glowRed");
        }

        public static GameObject FloorMedallion(Transform parent, Vector3 pos, float radius, string text)
        {
            var root = MuseumKit.Group(parent, "Medallion", pos);
            MuseumKit.Cyl(root.transform, "Ring", new Vector3(0, 0.012f, 0), radius, 0.02f, "brass");
            MuseumKit.Cyl(root.transform, "Field", new Vector3(0, 0.014f, 0), radius * 0.86f, 0.02f, "marbleDark");
            MuseumKit.Cyl(root.transform, "Inlay", new Vector3(0, 0.016f, 0), radius * 0.5f, 0.02f, "brassDark");
            MuseumKit.Label(root.transform, "Mono", new Vector3(0, 0.03f, 0), new Vector2(radius * 1.4f, radius * 1.4f), text,
                MuseumKit.TextRole.Title, MuseumKit.BrassText, TextAlignmentOptions.Center, new Vector3(90, 0, 0));
            return root;
        }

        // =====================================================================
        //  Wire the body text of a plaque to fade in on approach.
        // =====================================================================
        private static void WireBodyReveal(GameObject host, TMP_Text body, string copy, bool always)
        {
            var pc = host.AddComponent<PlaqueController>();
            var so = new SerializedObject(pc);
            SetObj(so, "_bodyField", body);
            SetStr(so, "_body", copy);
            // Wider reveal so boards are legible at a normal walking distance instead
            // of reading as faded "gray" until you are right on top of them.
            SetFloat(so, "_revealRadius", 14f);
            SetFloat(so, "_fullRadius", 7f);
            SetBool(so, "_alwaysVisible", always);
            SetBool(so, "_billboardYaw", false);
            SetBool(so, "_fixedFacing", true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObj(SerializedObject so, string p, Object v) { var x = so.FindProperty(p); if (x != null) x.objectReferenceValue = v; }
        private static void SetStr(SerializedObject so, string p, string v) { var x = so.FindProperty(p); if (x != null) x.stringValue = v; }
        private static void SetFloat(SerializedObject so, string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; }
        private static void SetBool(SerializedObject so, string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; }

        // =====================================================================
        //  Utilities
        // =====================================================================
        public static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return "x";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' && sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                if (sb.Length >= 24) break;
            }
            return sb.Length == 0 ? "x" : sb.ToString();
        }

        private static string Initials(string name)
        {
            var parts = name.Split(new[] { ' ', '·', '&' }, System.StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts) { if (char.IsLetter(p[0])) sb.Append(char.ToUpper(p[0])); if (sb.Length >= 3) break; }
            return sb.ToString();
        }
    }
}
