// -----------------------------------------------------------------------------
//  NarratorVoice.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A calm, slightly cryptic guide who greets the player at the start of every
//  room with one or two spoken sentences. This is the Unity/Quest translation of
//  the Web Speech API (SpeechSynthesis): on Android the device's TextToSpeech
//  engine is the direct equivalent. It speaks through the headset's own audio
//  output (2D, head-locked, never positioned in the world), exactly like
//  speechSynthesis would.
//
//  Behaviour mirrors the requested SpeechSynthesisUtterance setup:
//    * rate 0.9, pitch 0.85, volume 1.0 - an intimate, unhurried voice.
//    * prefer a deep English male voice; fall back to the first English voice.
//    * fire 1.2s after room entry so the scene settles before the voice speaks.
//    * cancel any in-progress line (stop / QUEUE_FLUSH) before the next one so
//      lines never overlap across a transition.
//
//  Purely additive: it never advances rooms, solves puzzles, or touches the
//  caption layer (NarrationController), the panel system, or time-scaling.
//  In the editor (no Android TTS) it simply logs the line it would have spoken.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using UnityEngine;

namespace Decrypted.Managers
{
    [DisallowMultipleComponent]
    public class NarratorVoice : MonoBehaviour
    {
        [Header("Voice (Android TextToSpeech = Web Speech equivalent)")]
        [Range(0.5f, 2f)] [SerializeField] private float _rate = 0.9f;
        [Range(0.5f, 2f)] [SerializeField] private float _pitch = 0.85f;
        [Range(0f, 1f)]   [SerializeField] private float _volume = 1.0f;
        [Tooltip("Seconds after entering a room before the narrator speaks.")]
        [SerializeField] private float _introDelay = 1.2f;
        [Tooltip("Prefer a deep English male voice if the engine exposes one.")]
        [SerializeField] private bool _preferMaleVoice = true;

        // One short, atmospheric greeting per room, tied to that room's content.
        // No em dashes anywhere; commas and periods only.
        private static readonly Dictionary<MuseumState, string> Lines = new()
        {
            { MuseumState.Splash,
                "Everything here has been kept quiet for a very long time. That ends the moment you move." },
            { MuseumState.Atrium,
                "Four rooms, four secrets, and only one way through. Take your time. They have waited longer than you have." },
            { MuseumState.AncientRoom,
                "Caesar trusted a single turn of a wheel to guard an empire. Move it three steps and listen." },
            { MuseumState.WWIIRoom,
                "They called this machine perfect. Three small wheels say otherwise. Find the letters it is hiding." },
            { MuseumState.VaultRoom,
                "Everything the other rooms protected sits behind this door. One word opens it, and you already carry it." },
            { MuseumState.RevealChamber,
                "Watch closely now. Every secret you broke tonight was the same secret, wearing a different face." },
            { MuseumState.Complete,
                "The history is yours to keep. Carry it quietly, the way it was always meant to travel." },
        };

        private Coroutine _pending;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _tts;
        private bool _ready;
#endif

        // -------------------------------------------------------------- lifecycle

        private void OnEnable()
        {
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Subscribe<StateChangedEvent>(OnStateChanged);
            InitEngine();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Unsubscribe<StateChangedEvent>(OnStateChanged);
            ShutdownEngine();
        }

        // Splash is set instantly at boot (no RoomEnteredEvent), so greet via state.
        private void OnStateChanged(StateChangedEvent e)
        {
            if (e.Current == MuseumState.Splash) Schedule(MuseumState.Splash);
        }

        private void OnRoomEntered(RoomEnteredEvent e) => Schedule(e.Room);

        private void Schedule(MuseumState room)
        {
            if (!Lines.TryGetValue(room, out var line)) return;
            CancelSpeech();                                  // cancel() before the new line
            if (_pending != null) StopCoroutine(_pending);
            _pending = StartCoroutine(SpeakAfter(_introDelay, line));
        }

        private IEnumerator SpeakAfter(float delay, string line)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            Speak(line);
            _pending = null;
        }

        // ------------------------------------------------------------ TTS engine

        private void InitEngine()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener(this));
            }
            catch (System.Exception ex) { Debug.LogWarning("[Narrator] TTS init failed: " + ex.Message); }
#endif
        }

        private void ShutdownEngine()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (_tts != null) { _tts.Call("stop"); _tts.Call("shutdown"); _tts.Dispose(); _tts = null; _ready = false; } }
            catch { }
#endif
        }

        private void CancelSpeech()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (_tts != null && _ready) _tts.Call<int>("stop"); } catch { }
#endif
        }

        private void Speak(string line)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_tts == null || !_ready) return;
                using (var bundle = new AndroidJavaObject("android.os.Bundle"))
                {
                    bundle.Call("putFloat", "volume", _volume);     // KEY_PARAM_VOLUME
                    _tts.Call<int>("speak", line, 0, bundle, "decrypted_narration"); // 0 = QUEUE_FLUSH
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[Narrator] speak failed: " + ex.Message); }
#else
            Debug.Log("[Narrator] (TTS) \"" + line + "\"");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Called by the engine when initialization completes (status 0 == SUCCESS).
        private void OnEngineReady(int status)
        {
            if (status != 0)
            {
                Debug.LogWarning("[Narrator] no usable TTS engine on device (status " + status + "); narration will be silent.");
                return;
            }
            try
            {
                ConfigureVoice();
                _tts.Call<int>("setSpeechRate", _rate);
                _tts.Call<int>("setPitch", _pitch);
                _ready = true;
            }
            catch (System.Exception ex) { Debug.LogWarning("[Narrator] config failed: " + ex.Message); }
        }

        private void ConfigureVoice()
        {
            // English language, then prefer a deep male English voice if exposed.
            using (var enLocale = new AndroidJavaObject("java.util.Locale", "en"))
                _tts.Call<int>("setLanguage", enLocale);

            if (!_preferMaleVoice) return;
            try
            {
                var voices = _tts.Call<AndroidJavaObject>("getVoices"); // Set<Voice>
                if (voices == null) return;
                var iter = voices.Call<AndroidJavaObject>("iterator");
                AndroidJavaObject best = null, firstEn = null;
                while (iter.Call<bool>("hasNext"))
                {
                    var v = iter.Call<AndroidJavaObject>("next");
                    string vname = v.Call<string>("getName") ?? "";
                    var loc = v.Call<AndroidJavaObject>("getLocale");
                    string lang = loc != null ? loc.Call<string>("getLanguage") : "";
                    string low = vname.ToLowerInvariant();
                    bool isEn = lang == "en" || low.StartsWith("en");
                    if (!isEn) continue;
                    if (firstEn == null) firstEn = v;
                    if (low.Contains("male") && !low.Contains("female")) { best = v; break; }
                }
                var chosen = best ?? firstEn;
                if (chosen != null) _tts.Call<int>("setVoice", chosen);
            }
            catch (System.Exception ex) { Debug.LogWarning("[Narrator] voice scan skipped: " + ex.Message); }
        }

        private class InitListener : AndroidJavaProxy
        {
            private readonly NarratorVoice _owner;
            public InitListener(NarratorVoice owner) : base("android.speech.tts.TextToSpeech$OnInitListener") { _owner = owner; }
            void onInit(int status) { _owner.OnEngineReady(status); }
        }
#endif
    }
}
