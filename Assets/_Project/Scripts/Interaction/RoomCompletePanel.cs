// -----------------------------------------------------------------------------
//  RoomCompletePanel.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A floating, world-space "room complete" notification. It is spawned by the
//  ProgressionGateController the instant a room's win condition is met, anchored
//  to the player's CURRENT head pose (not a fixed world coordinate) so it always
//  appears at the player's real eye level and in front of wherever they are
//  looking. It billboards to keep facing the player and carries a single physical
//  "Let's Go" confirm button.
//
//  Progression is entirely manual: nothing advances until the player physically
//  presses that button with the collision/grip hand interaction (the button is a
//  PokeButton with a 3D collider; the XR Direct Interactor selects it on grip).
//
//  The panel builds its own visuals in code (world-space Canvas for the flat
//  plane + two text lines + button face) so it needs no prefab asset. Everything
//  uses unscaled time so the world time-scaling system never affects it.
// -----------------------------------------------------------------------------

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class RoomCompletePanel : MonoBehaviour
    {
        private Transform _player;     // the XR camera (head) we billboard toward
        private Action _onConfirm;
        private bool _fired;
        private PokeButton _button;

        /// <summary>Build and place the panel, then start billboarding.</summary>
        public void Show(string headline, string prompt, Vector3 position, Transform player,
                         TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Action onConfirm)
        {
            _player = player;
            _onConfirm = onConfirm;
            transform.position = position;
            Build(headline, prompt, titleFont, bodyFont);
            Face(true);
        }

        private void Build(string headline, string prompt, TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
        {
            // ---- Flat panel: a world-space Canvas (0.001 scale => sizeDelta in mm) ----
            var canvasGO = new GameObject("Canvas", typeof(Canvas));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(960f, 560f);
            rt.localScale = Vector3.one * 0.001f;     // ~0.96m x 0.56m
            rt.localPosition = Vector3.zero;

            NewImage("BG", rt, Vector2.zero, new Vector2(960, 560), new Color(0.06f, 0.07f, 0.10f, 0.94f));
            NewImage("AccentTop", rt, new Vector2(0, 244), new Vector2(960, 10), new Color(0.10f, 0.75f, 0.75f, 1f));

            NewText("Headline", rt, new Vector2(0, 150), new Vector2(900, 170), headline, 96,
                    titleFont, new Color(0.96f, 0.93f, 0.84f, 1f));
            NewText("Prompt", rt, new Vector2(0, 6), new Vector2(880, 150), prompt, 54,
                    bodyFont, new Color(0.86f, 0.89f, 0.93f, 1f));

            // Button face (purely visual).
            var face = NewImage("BtnFace", rt, new Vector2(0, -188), new Vector2(440, 150),
                                new Color(0.10f, 0.70f, 0.70f, 1f));
            NewText("BtnLabel", face.rectTransform, Vector2.zero, new Vector2(440, 150), "Let's Go", 64,
                    bodyFont, Color.white);

            // ---- Physical confirm button: a 3D collider the hand can grip ----
            // Placed at the visual button's world position, nudged toward the
            // player (+Z is the panel's front) so the hand can reach its face.
            var btnGO = new GameObject("ConfirmButton");
            btnGO.transform.SetParent(transform, false);
            btnGO.transform.localPosition = new Vector3(0f, -0.188f, 0.04f);
            var box = btnGO.AddComponent<BoxCollider>();
            box.size = new Vector3(0.46f, 0.16f, 0.12f);
            _button = btnGO.AddComponent<PokeButton>();
            _button.OnPressed += OnButtonPressed;
        }

        private Image NewImage(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                                        string text, float fontSize, TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        private void OnButtonPressed(string _) => Confirm();

        /// <summary>Fire the transition once, then remove the panel.</summary>
        public void Confirm()
        {
            if (_fired) return;
            _fired = true;
            if (_button != null) _button.OnPressed -= OnButtonPressed;
            var cb = _onConfirm;
            _onConfirm = null;
            cb?.Invoke();
            Destroy(gameObject);
        }

        private void LateUpdate() => Face(false);

        // Billboard: rotate so the canvas front (+Z) keeps pointing at the player,
        // kept upright (no pitch/roll). Unscaled so time-scaling never freezes it.
        private void Face(bool snap)
        {
            if (_player == null) return;
            Vector3 dir = _player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-5f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = snap
                ? target
                : Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        }
    }
}
