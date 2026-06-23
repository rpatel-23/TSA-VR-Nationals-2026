// -----------------------------------------------------------------------------
//  NextRoomButton.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  The MANUAL progression control for normal play. When a room is ready to advance
//  (its puzzle solved, or the Atrium dwell elapsed) AutoProgressionController spawns
//  this world-space panel instead of the auto-countdown. It shows a headline and a
//  big "Move to Next Room" button that the player presses (finger poke OR ray
//  click) when THEY are ready - nothing auto-advances. Demo Mode still uses the
//  CountdownPanel so the recorded showcase stays hands-free.
//
//  Built procedurally to match the CountdownPanel/world-label look: a world-space
//  canvas for the visuals plus a 3D BoxCollider + PokeButton as the actual press
//  target (so both the poke and ray interactors on the rig can trigger it). It is
//  billboarded to face the player and runs on unscaled time.
// -----------------------------------------------------------------------------

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class NextRoomButton : MonoBehaviour
    {
        private Transform _player;
        private Action _onPressed;
        private bool _fired;

        public void Show(string headline, Vector3 position, Transform player,
                         TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Action onPressed)
        {
            _player = player;
            _onPressed = onPressed;
            transform.position = position;
            Build(headline, titleFont, bodyFont);
            Face(true);
        }

        private void Press()
        {
            if (_fired) return;
            _fired = true;
            var cb = _onPressed;
            _onPressed = null;
            cb?.Invoke();          // runs ScreenFader transition + GameManager.Advance()
            Destroy(gameObject);   // clean up the panel
        }

        private void LateUpdate() => Face(false);

        private void Face(bool snap)
        {
            if (_player == null) return;
            // +Z points away from the player so the text reads correctly (see CountdownPanel).
            Vector3 dir = transform.position - _player.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-5f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = snap ? target
                : Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        }

        private void Build(string headline, TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 520f);
            rt.localScale = Vector3.one * 0.001f;     // ~0.9m x 0.52m
            rt.localPosition = Vector3.zero;

            NewImage("BG", rt, Vector2.zero, new Vector2(900, 520), new Color(0.06f, 0.07f, 0.10f, 0.93f));
            NewImage("Accent", rt, new Vector2(0, 225), new Vector2(900, 8), new Color(0.10f, 0.75f, 0.75f, 1f));
            NewText("Headline", rt, new Vector2(0, 130), new Vector2(840, 170), headline, 80, titleFont,
                    new Color(0.96f, 0.93f, 0.84f, 1f));

            // Button visual: a teal rounded slab with a white label.
            var btn = NewImage("Button", rt, new Vector2(0, -120), new Vector2(640, 190), new Color(0.10f, 0.58f, 0.58f, 1f));
            NewText("BtnLabel", btn.rectTransform, Vector2.zero, new Vector2(600, 160),
                    "Move to Next Room  ▶", 54, bodyFont, Color.white);

            // 3D press target aligned to the button visual (button center y = -120 * 0.001 = -0.12 m,
            // size 640x190 * 0.001 = 0.64 x 0.19 m). Both the poke and ray interactors fire its select.
            var press = new GameObject("PressTarget");
            press.transform.SetParent(transform, false);
            press.transform.localPosition = new Vector3(0f, -0.12f, -0.02f);
            press.transform.localRotation = Quaternion.identity;
            var bc = press.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.64f, 0.19f, 0.08f);
            var poke = press.AddComponent<PokeButton>();
            poke.OnPressed += _ => Press();
        }

        private Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, Vector2 pos, Vector2 size, string text,
                                        float fontSize, TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = fontSize; t.color = color;
            t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = true; t.raycastTarget = false;
            return t;
        }
    }
}
