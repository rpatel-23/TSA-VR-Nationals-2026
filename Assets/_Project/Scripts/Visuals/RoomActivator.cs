// -----------------------------------------------------------------------------
//  RoomActivator.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Sits on each room's root GameObject. The SceneController toggles the root
//  active/inactive for coarse room-based culling, then calls OnActivated() so
//  the room can do its "come alive" work in a controlled order:
//
//   * Kick off baked-light-friendly emissive/anim sequences.
//   * Start the room's ambient bed and apply its mixer snapshot.
//   * Fire an optional timeline/animator "intro" (e.g. lights warming up).
//   * Enable only this room's reflection probe.
//
//  It also exposes PreWarm(), which the SceneController can call on the *next*
//  room a step ahead of time. PreWarm forces shaders/meshes resident (one hidden
//  render) without running the full activation, eliminating first-frame hitches
//  when the player actually transitions in.
//
//  Everything here is defensive: each reference is optional so a half-dressed
//  room in the editor still behaves.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Managers;
using UnityEngine;

namespace Decrypted.Visuals
{
    [DisallowMultipleComponent]
    public class RoomActivator : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Which museum state this room represents (for ambience + snapshot).")]
        [SerializeField] private MuseumState _state = MuseumState.Atrium;

        [Header("Audio")]
        [Tooltip("AudioManager key for this room's ambient bed. Leave empty to skip.")]
        [SerializeField] private string _ambientKey = "amb_atrium";
        [Tooltip("Mixer snapshot to blend to on entry. Leave empty to skip.")]
        [SerializeField] private string _mixerSnapshot = "";
        [SerializeField] private float _snapshotBlend = 1.5f;

        [Header("Come-alive sequence")]
        [Tooltip("Animator played once when the room activates (light warm-up etc.).")]
        [SerializeField] private Animator _introAnimator;
        [SerializeField] private string _introTrigger = "Activate";

        [Tooltip("Emissive renderers faded up on activation (e.g. wall sconces).")]
        [SerializeField] private List<Renderer> _emissiveRenderers = new List<Renderer>();
        [SerializeField] private float _emissiveFadeSeconds = 1.2f;
        [SerializeField] private float _emissiveTarget = 1.0f;

        [Tooltip("Lights enabled on activation (kept off otherwise to save culling work).")]
        [SerializeField] private List<Light> _roomLights = new List<Light>();

        [Tooltip("Particle systems / animators to play on activation (subtle dust motes, etc.).")]
        [SerializeField] private List<ParticleSystem> _ambientParticles = new List<ParticleSystem>();

        [Header("Reflection")]
        [SerializeField] private ReflectionProbe _reflectionProbe;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private bool _prewarmed;
        private Coroutine _emissiveRoutine;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            // Start lights off; they cost culling work even when the root is active.
            foreach (var l in _roomLights) if (l != null) l.enabled = false;
        }

        /// <summary>
        /// Full activation. Called by SceneController once the room root is enabled
        /// and the player has been placed.
        /// </summary>
        public void OnActivated()
        {
            // Lights on.
            foreach (var l in _roomLights) if (l != null) l.enabled = true;

            // Reflection probe (only this room's).
            if (_reflectionProbe != null) _reflectionProbe.gameObject.SetActive(true);

            // Ambient bed + mixer mood. AudioManager also reacts to StateChanged,
            // but doing it here keeps a room self-contained if used in isolation.
            if (AudioManager.Instance != null)
            {
                if (!string.IsNullOrEmpty(_ambientKey)) AudioManager.Instance.SetAmbient(_ambientKey);
                if (!string.IsNullOrEmpty(_mixerSnapshot))
                    AudioManager.Instance.TransitionSnapshot(_mixerSnapshot, _snapshotBlend);
            }

            // Intro animation (lights warming, banners unfurling…).
            if (_introAnimator != null && !string.IsNullOrEmpty(_introTrigger))
                _introAnimator.SetTrigger(_introTrigger);

            // Subtle ambient particles.
            foreach (var ps in _ambientParticles) if (ps != null) ps.Play(true);

            // Emissive fade-up.
            if (_emissiveRenderers.Count > 0)
            {
                if (_emissiveRoutine != null) StopCoroutine(_emissiveRoutine);
                _emissiveRoutine = StartCoroutine(FadeEmissive(0f, _emissiveTarget, _emissiveFadeSeconds));
            }

            // Per-room music crossfade + narrative voice (both no-op if absent).
            RoomMusicController.Instance?.OnRoomEntered(_state);
            RoomNarratorController.Instance?.OnRoomEntered(_state);
        }

        /// <summary>
        /// Cheap residency pass for the *next* room. Briefly enables the root just
        /// long enough for the render pipeline to compile shaders and upload
        /// meshes/lightmaps, then disables it again. Prevents the first-frame
        /// hitch a player would otherwise feel when transitioning in.
        /// </summary>
        public IEnumerator PreWarm()
        {
            if (_prewarmed) yield break;
            bool wasActive = gameObject.activeSelf;
            if (!wasActive) gameObject.SetActive(true);

            // Wait two frames so the renderer submits the room at least once.
            yield return null;
            yield return null;

            _prewarmed = true;
            if (!wasActive) gameObject.SetActive(false);
        }

        // -------------------------------------------------------------- internal

        private IEnumerator FadeEmissive(float from, float to, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, seconds);
                float k = Mathf.SmoothStep(from, to, t);
                ApplyEmissive(k);
                yield return null;
            }
            ApplyEmissive(to);
        }

        private void ApplyEmissive(float intensity)
        {
            foreach (var r in _emissiveRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                // Multiply the material's authored emission tint by our scalar.
                Color baseEmission = r.sharedMaterial != null && r.sharedMaterial.HasProperty(EmissionColorID)
                    ? r.sharedMaterial.GetColor(EmissionColorID)
                    : Color.white;
                _mpb.SetColor(EmissionColorID, baseEmission * intensity);
                r.SetPropertyBlock(_mpb);
            }
        }

        public MuseumState State => _state;
    }
}
