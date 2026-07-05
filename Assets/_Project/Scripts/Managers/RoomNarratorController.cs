// -----------------------------------------------------------------------------
//  RoomNarratorController.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  The museum's narrative voice. RoomActivator calls OnRoomEntered when a room
//  activates; 1.5 s later (unscaled) the room's pre-baked VO clip plays through a
//  dedicated 2D (head-locked) AudioSource at 0.85 volume, so it always reaches the
//  player regardless of facing. The narrator never overlaps itself - any clip in
//  progress is cancelled first. While a line plays the music is ducked.
//
//  A head-locked TextMeshPro subtitle (2.0 m ahead, 0.4 m below eye level) shows
//  the spoken text for the clip's duration + 1.0 s, then fades - so the narration
//  reads even if the VO is silent on a given device (the shipped clips are silent
//  placeholders; drop real recordings in by key vo_*).
//
//  Lines: a calm, warm, authoritative museum guide. Short sentences. No em dashes.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Util;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

namespace Decrypted.Managers
{
    [DefaultExecutionOrder(-80)]
    public class RoomNarratorController : Singleton<RoomNarratorController>
    {
        [SerializeField] private float _startDelay = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.85f;
        [SerializeField] private float _captionTail = 1.0f;
        [SerializeField] private TMP_FontAsset _font;

        private static readonly Dictionary<MuseumState, (string key, string line)> Lines = new()
        {
            { MuseumState.Splash, ("vo_splash",
                "Welcome to DECRYPTED. You are about to walk through three thousand years of secret writing. Take your time, and touch everything.") },
            { MuseumState.Atrium, ("vo_atrium",
                "This is the heart of the museum. Four doors, four eras. Each one holds a code, and each code is yours to break.") },
            { MuseumState.AncientRoom, ("vo_ancient",
                "Welcome to the ancient world. The Romans had a secret, and it started with a simple twist of the alphabet. Turn the disk and find it.") },
            { MuseumState.WWIIRoom, ("vo_wwii",
                "This machine changed the course of a war. Set its three wheels, type the message, and see if you can read what it was built to hide.") },
            { MuseumState.VaultRoom, ("vo_vault",
                "Now the secret becomes a lock. Modern codes do not hide a message, they seal it. Enter the word you just recovered to open the vault.") },
            { MuseumState.RevealChamber, ("vo_reveal",
                "One idea has carried you across three thousand years. Watch it take shape, from a carved letter to a living circuit.") },
            { MuseumState.Complete, ("vo_complete",
                "You did it. From Caesar to the digital age, you followed the unbroken thread. Thank you for visiting DECRYPTED.") },
        };

        private AudioSource _vo;
        private CanvasGroup _captionGroup;
        private TextMeshProUGUI _captionText;
        private Coroutine _routine;

        protected override void OnSingletonAwake()
        {
            _vo = gameObject.AddComponent<AudioSource>();
            _vo.playOnAwake = false;
            _vo.loop = false;
            _vo.spatialBlend = 0f;       // 2D, head-locked
            _vo.volume = _volume;
        }

        /// <summary>Called by RoomActivator on room entry.</summary>
        public void OnRoomEntered(MuseumState room)
        {
            if (!Lines.ContainsKey(room)) return;
            if (_routine != null) StopCoroutine(_routine);
            if (_vo != null && _vo.isPlaying) _vo.Stop();   // never overlap
            _routine = StartCoroutine(Speak(room));
        }

        private IEnumerator Speak(MuseumState room)
        {
            var (key, line) = Lines[room];

            float t = 0f;
            while (t < _startDelay) { t += Time.unscaledDeltaTime; yield return null; }

            float dur = 4f;
            var clip = AudioManager.Instance != null ? AudioManager.Instance.GetClip(key) : null;
            if (clip != null)
            {
                _vo.clip = clip;
                _vo.volume = _volume;
                _vo.Play();
                dur = clip.length;
            }

            // Duck the music for the spoken line (+ tail).
            RoomMusicController.Instance?.DuckFor(dur + _captionTail);

            EnsureCaption();
            _captionText.text = line;
            yield return Fade(_captionGroup, 0f, 1f, 0.35f);

            float hold = 0f;
            while (hold < dur + _captionTail) { hold += Time.unscaledDeltaTime; yield return null; }

            yield return Fade(_captionGroup, 1f, 0f, 0.6f);
            _routine = null;
        }

        private void EnsureCaption()
        {
            if (_captionGroup != null) return;
            var o = FindObjectOfType<XROrigin>();
            Transform head = (o != null && o.Camera != null) ? o.Camera.transform : (Camera.main != null ? Camera.main.transform : null);

            var go = new GameObject("NarrationCaption", typeof(Canvas));
            if (head != null) go.transform.SetParent(head, false);   // head-locked
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1400f, 300f);
            rt.localScale = Vector3.one * 0.001f;
            rt.localPosition = new Vector3(0f, -0.35f, 1.5f);   // 1.5 m ahead, 0.35 m below eye (pulled closer)
            rt.localRotation = Quaternion.identity;
            _captionGroup = go.AddComponent<CanvasGroup>();
            _captionGroup.alpha = 0f;

            var bg = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(rt, false);
            var brt = bg.GetComponent<RectTransform>(); brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(1400f, 300f);
            var img = bg.GetComponent<Image>(); img.color = new Color(0.04f, 0.05f, 0.07f, 0.72f); img.raycastTarget = false;

            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(rt, false);
            var trt = t.GetComponent<RectTransform>(); trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(1320f, 260f);
            if (_font != null) t.font = _font;
            t.fontSize = 54; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = true; t.raycastTarget = false;
            var mat = t.fontMaterial;
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.02f, 0.03f, 1f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
            _captionText = t;
        }

        private IEnumerator Fade(CanvasGroup g, float from, float to, float seconds)
        {
            if (g == null) yield break;
            float t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds); g.alpha = Mathf.Lerp(from, to, t); yield return null; }
            g.alpha = to;
        }
    }
}
