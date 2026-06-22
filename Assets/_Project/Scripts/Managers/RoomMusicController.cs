// -----------------------------------------------------------------------------
//  RoomMusicController.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  Per-room themed music. RoomActivator calls OnRoomEntered on room entry; this
//  crossfades from the outgoing loop to the room's incoming loop over 2.5 s (two
//  2D AudioSources, Mathf.Lerp on their volumes, never an abrupt cut), then stops
//  the outgoing source. Both sources loop seamlessly.
//
//  Music sits at an exploration volume and DUCKS automatically while the narrator
//  speaks (RoomNarratorController.DuckFor), easing back afterwards. All fades use
//  unscaled time so the SUPERHOT-style time-scaling never affects the music.
//
//  Clips are the pre-baked synth loops keyed mus_* (Tooling/Audio).
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Util;
using UnityEngine;

namespace Decrypted.Managers
{
    [DefaultExecutionOrder(-80)]
    public class RoomMusicController : Singleton<RoomMusicController>
    {
        [SerializeField, Range(0f, 1f)] private float _exploreVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _duckVolume = 0.25f;
        [SerializeField] private float _crossfade = 2.5f;

        private static readonly Dictionary<MuseumState, string> Keys = new()
        {
            { MuseumState.Splash, "mus_splash" }, { MuseumState.Atrium, "mus_atrium" },
            { MuseumState.AncientRoom, "mus_ancient" }, { MuseumState.WWIIRoom, "mus_wwii" },
            { MuseumState.VaultRoom, "mus_vault" }, { MuseumState.RevealChamber, "mus_reveal" },
            { MuseumState.Complete, "mus_complete" },
        };

        private AudioSource _a, _b;
        private AudioSource _active;
        private string _currentKey = "";
        private float _duckMul = 1f;
        private Coroutine _crossRoutine, _duckRoutine;

        private float TargetVol => _exploreVolume * _duckMul;

        protected override void OnSingletonAwake()
        {
            _a = NewSource("Music_A");
            _b = NewSource("Music_B");
            _active = _a;
        }

        private AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        /// <summary>Called by RoomActivator on room entry.</summary>
        public void OnRoomEntered(MuseumState room)
        {
            if (!Keys.TryGetValue(room, out var key)) return;
            if (key == _currentKey) return;
            var clip = AudioManager.Instance != null ? AudioManager.Instance.GetClip(key) : null;
            if (clip == null) return;
            _currentKey = key;
            if (_crossRoutine != null) StopCoroutine(_crossRoutine);
            _crossRoutine = StartCoroutine(Crossfade(clip));
        }

        private IEnumerator Crossfade(AudioClip next)
        {
            var from = _active;
            var to = (_active == _a) ? _b : _a;
            _active = to;

            to.clip = next; to.volume = 0f; to.Play();
            float fromStart = from.volume;
            float t = 0f;
            while (t < _crossfade)
            {
                t += Time.unscaledDeltaTime;
                float k = t / _crossfade;
                to.volume = Mathf.Lerp(0f, TargetVol, k);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                yield return null;
            }
            to.volume = TargetVol;
            from.volume = 0f; from.Stop();
            _crossRoutine = null;
        }

        /// <summary>Duck the music to the duck volume for `seconds`, then ease back.</summary>
        public void DuckFor(float seconds)
        {
            if (_duckRoutine != null) StopCoroutine(_duckRoutine);
            _duckRoutine = StartCoroutine(Duck(seconds));
        }

        private IEnumerator Duck(float seconds)
        {
            float ducked = (_exploreVolume > 0.0001f) ? _duckVolume / _exploreVolume : 1f; // so TargetVol == _duckVolume
            yield return LerpDuck(ducked, 0.4f);   // down to duck
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            yield return LerpDuck(1f, 0.6f);       // back up to explore
            _duckRoutine = null;
        }

        private IEnumerator LerpDuck(float to, float seconds)
        {
            float from = _duckMul;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                _duckMul = Mathf.Lerp(from, to, t);
                ApplyActiveVolume();
                yield return null;
            }
            _duckMul = to;
            ApplyActiveVolume();
        }

        private void ApplyActiveVolume()
        {
            // Only drive the active source directly when not mid-crossfade.
            if (_crossRoutine == null && _active != null) _active.volume = TargetVol;
        }
    }
}
