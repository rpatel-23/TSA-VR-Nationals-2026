// -----------------------------------------------------------------------------
//  SceneController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Owns the *physical* presentation of progression:
//   * Room-based activation/deactivation (only the active room + its neighbour
//     preload are enabled — critical for Quest 1 draw-call/memory budgets).
//   * Player rig placement at each room's anchor.
//   * A VR-safe screen fade (camera-attached quad) for clean transitions.
//   * Reflection-probe and ambient-audio hand-off via the active RoomDescriptor.
//
//  The museum is ONE Unity scene. We do not load scenes additively at runtime
//  (load hitches are jarring in VR and risk dropping below 72 FPS). Instead each
//  room is a child hierarchy toggled by a RoomActivator.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Visuals;
using UnityEngine;

namespace Decrypted.Core
{
    public class SceneController : MonoBehaviour
    {
        [Header("Player Rig")]
        [Tooltip("Root of the XR Origin (the thing we move between rooms).")]
        [SerializeField] private Transform _xrOrigin;
        [Tooltip("The HMD camera, used to anchor the fade quad and to compute headset offset.")]
        [SerializeField] private Camera _hmdCamera;

        [Header("Rooms")]
        [SerializeField] private List<RoomDescriptor> _rooms = new List<RoomDescriptor>();

        [Header("Fade")]
        [SerializeField] private ScreenFader _fader;
        [Tooltip("Seconds for fade-out and fade-in halves of a transition.")]
        [SerializeField] private float _fadeDuration = 0.6f;

        [Header("Pre-warm (kills the 'black screen then recovers' hitch)")]
        [Tooltip("A short while after entering a room, silently make the NEXT room's shaders/" +
                 "meshes GPU-resident so switching to it later doesn't stall on first render " +
                 "while the screen is black. Uncheck only if you ever see the upcoming room " +
                 "flicker into view for a frame.")]
        [SerializeField] private bool _preWarmNextRoom = true;
        [Tooltip("Seconds to wait after entering a room before pre-warming the next one.")]
        [SerializeField] private float _preWarmDelay = 1.5f;

        private readonly Dictionary<MuseumState, RoomDescriptor> _byState =
            new Dictionary<MuseumState, RoomDescriptor>();

        private MuseumState _active = MuseumState.Boot;

        private void Awake()
        {
            _byState.Clear();
            foreach (var r in _rooms)
                if (r != null && r.roomRoot != null) _byState[r.state] = r;

            // Start with everything off; GameManager will SnapTo the first state.
            foreach (var r in _rooms)
                if (r?.roomRoot != null) r.roomRoot.SetActive(false);
        }

        /// <summary>Immediate activation with no fade (boot/reset).</summary>
        public void SnapTo(MuseumState target)
        {
            ActivateRoom(target);
            PlacePlayer(target);
            _active = target;
        }

        /// <summary>Faded transition: fade out, swap rooms, place player, fade in.</summary>
        public IEnumerator TransitionTo(MuseumState target)
        {
            if (_fader != null) yield return _fader.FadeOut(_fadeDuration);

            // The room swap is synchronous; guard it so a throw here can NEVER skip the
            // fade-in below. A stuck-opaque fader would look exactly like a hard freeze
            // (black headset, no progression). On failure we log and still fade back in.
            try
            {
                ActivateRoom(target);
                PlacePlayer(target);
                _active = target;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SceneController] Room activation for {target} failed; revealing anyway. {e}");
            }

            // Give the GPU a couple of frames to warm up the now-visible room
            // before we reveal it (prevents a first-frame hitch in the headset).
            yield return null;
            yield return null;

            if (_fader != null) yield return _fader.FadeIn(_fadeDuration);
        }

        /// <summary>Make a room GPU-resident ahead of time WITHOUT fully activating it, so a
        /// later TransitionTo(target) doesn't stall on first-render shader compile + mesh
        /// upload while the screen is black (the "black then recovers" hitch on Quest). It
        /// briefly enables the (off-screen, one-step-away) room root for two frames so the
        /// renderer submits it once, then disables it again — RoomActivator.PreWarm does NOT
        /// run OnActivated, so no lights/audio/particles start during the warm-up.</summary>
        public void PreWarm(MuseumState target)
        {
            if (!_preWarmNextRoom) return;
            if (target == _active) return;
            if (!_byState.TryGetValue(target, out var room) || room.roomRoot == null) return;
            if (room.roomRoot.activeSelf) return; // already live; nothing to warm
            var activator = room.roomRoot.GetComponent<RoomActivator>();
            if (activator != null) StartCoroutine(PreWarmAfter(activator));
        }

        private IEnumerator PreWarmAfter(RoomActivator activator)
        {
            if (_preWarmDelay > 0f) yield return new WaitForSecondsRealtime(_preWarmDelay);
            yield return activator.PreWarm();
        }

        // --------------------------------------------------------- internals

        private void ActivateRoom(MuseumState target)
        {
            // Activate the target room and deactivate all others. (Pre-warming the next
            // room ahead of time is handled separately by PreWarm/PreWarmAfter.)
            foreach (var kvp in _byState)
            {
                bool isActive = kvp.Key == target;
                var room = kvp.Value;
                if (room.roomRoot.activeSelf != isActive)
                    room.roomRoot.SetActive(isActive);

                // Reflection probes: only the active one renders.
                if (room.reflectionProbe != null)
                    room.reflectionProbe.gameObject.SetActive(isActive);
            }

            if (_byState.TryGetValue(target, out var active))
            {
                // Let the RoomActivator do per-room enable work (lights, anim, audio).
                var activator = active.roomRoot.GetComponent<RoomActivator>();
                if (activator != null) activator.OnActivated();
                active.hasBeenVisited = true;
            }
        }

        private void PlacePlayer(MuseumState target)
        {
            if (_xrOrigin == null) return;
            if (!_byState.TryGetValue(target, out var room) || room.playerAnchor == null) return;

            // Move the rig so the *headset* (not the rig origin) lands on the anchor.
            // This compensates for the player having physically walked within the
            // guardian, which otherwise causes them to spawn off-mark.
            if (_hmdCamera != null)
            {
                Vector3 camOffset = _hmdCamera.transform.position - _xrOrigin.position;
                camOffset.y = 0f; // only recenter horizontally
                Vector3 placed = room.playerAnchor.position - camOffset;
                // Respect the rig's configured height (Inspector Y); do NOT snap the
                // rig down to the anchor's floor Y on every room entry. This is what
                // previously pushed the camera back to floor level after a transition.
                placed.y = _xrOrigin.position.y;
                _xrOrigin.position = placed;

                // Yaw the rig so the player faces the anchor's forward.
                float yawDelta = room.playerAnchor.eulerAngles.y - _hmdCamera.transform.eulerAngles.y;
                _xrOrigin.RotateAround(_hmdCamera.transform.position, Vector3.up, yawDelta);
            }
            else
            {
                // Preserve the rig's configured height; only place X/Z from the anchor.
                Vector3 pos = room.playerAnchor.position;
                pos.y = _xrOrigin.position.y;
                _xrOrigin.SetPositionAndRotation(pos, room.playerAnchor.rotation);
            }

            // Demo Mode has no headset to supply a standing height, so the camera would
            // otherwise sit on the floor. Frame it per room: lift the rig until the camera
            // EYE lands at the anchor's Y (authored ~5% above that room's main artifact).
            // Interactive (non-demo) play keeps the player's real floor-tracked height.
            if (GameManager.Instance != null && GameManager.Instance.DemoMode)
            {
                float eyeY = room.playerAnchor.position.y;
                if (_hmdCamera != null)
                    _xrOrigin.position += new Vector3(0f, eyeY - _hmdCamera.transform.position.y, 0f);
                else
                {
                    var p = _xrOrigin.position; p.y = eyeY; _xrOrigin.position = p;
                }
            }
        }

        public RoomDescriptor GetDescriptor(MuseumState state)
            => _byState.TryGetValue(state, out var r) ? r : null;
    }
}
