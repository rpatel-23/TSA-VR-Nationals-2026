// -----------------------------------------------------------------------------
//  CountdownPanel.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A small world-space "progressing in N" popup shown for a few seconds before an
//  automatic room advance. It is NOT interactive (no button, not a notification
//  panel the player must dismiss) - it is purely a visible countdown so the auto
//  advance never feels like a jump cut.
//
//  Spawned by AutoProgressionController in front of the player's current head pose
//  and billboarded to face them. The countdown runs on UNSCALED time so the
//  SUPERHOT-style time-scaling (which drives timeScale -> 0 when the player is
//  still) can never freeze it mid-count. When it reaches zero it invokes the
//  supplied callback (which runs the ScreenFader transition + advance) and then
//  destroys itself. Styling matches the world-space label/plaque look.
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class CountdownPanel : MonoBehaviour
    {
        private Transform _player;
        private TextMeshProUGUI _countLine;
        private Action _onComplete;
        private int _seconds;
        private bool _fired;

        public void Show(string headline, int seconds, Vector3 position, Transform player,
                         TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Action onComplete)
        {
            _player = player;
            _onComplete = onComplete;
            _seconds = Mathf.Max(1, seconds);
            transform.position = position;
            Build(headline, titleFont, bodyFont);
            Face(true);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            for (int n = _seconds; n >= 1; n--)
            {
                if (_countLine != null) _countLine.text = "Progressing in " + n;
                // Unscaled: immune to the time-scaling system pausing scaled time.
                yield return new WaitForSecondsRealtime(1f);
            }
            Complete();
        }

        private void Complete()
        {
            if (_fired) return;
            _fired = true;
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke();          // runs ScreenFader transition + GameManager.Advance()
            Destroy(gameObject);   // clean up the popup
        }

        private void LateUpdate() => Face(false);

        private void Face(bool snap)
        {
            if (_player == null) return;
            Vector3 dir = _player.position - transform.position;   // canvas front (+Z) faces player
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
            rt.sizeDelta = new Vector2(900f, 480f);
            rt.localScale = Vector3.one * 0.001f;     // ~0.9m x 0.48m
            rt.localPosition = Vector3.zero;

            NewImage("BG", rt, Vector2.zero, new Vector2(900, 480), new Color(0.06f, 0.07f, 0.10f, 0.93f));
            NewImage("Accent", rt, new Vector2(0, 200), new Vector2(900, 8), new Color(0.10f, 0.75f, 0.75f, 1f));
            NewText("Headline", rt, new Vector2(0, 90), new Vector2(840, 180), headline, 90, titleFont, new Color(0.96f, 0.93f, 0.84f, 1f));
            _countLine = NewText("Count", rt, new Vector2(0, -110), new Vector2(840, 140), "Progressing in " + _seconds, 56, bodyFont, new Color(0.86f, 0.89f, 0.93f, 1f));
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

        private TextMeshProUGUI NewText(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, TMP_FontAsset font, Color color)
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
