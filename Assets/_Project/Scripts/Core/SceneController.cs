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

        [Header("Pacing")]
        [Tooltip("Default delay between solving an exhibit and the room transition.")]
        [SerializeField] private float _defaultExitDelay = 2.5f;
        [Tooltip("Per-room overrides (lets the WWII 'power up' sequence breathe).")]
        [SerializeField] private List<RoomExitDelay> _exitDelays = new List<RoomExitDelay>();

        [System.Serializable]
        public struct RoomExitDelay { public MuseumState state; public float delay; }

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

        public float GetExitDelay(MuseumState state)
        {
            foreach (var e in _exitDelays) if (e.state == state) return e.delay;
            return _defaultExitDelay;
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

            ActivateRoom(target);
            PlacePlayer(target);
            _active = target;

            // Give the GPU a couple of frames to warm up the now-visible room
            // before we reveal it (prevents a first-frame hitch in the headset).
            yield return null;
            yield return null;

            if (_fader != null) yield return _fader.FadeIn(_fadeDuration);
        }

        // --------------------------------------------------------- internals

        private void ActivateRoom(MuseumState target)
        {
            // Activate target; deactivate all others. We also pre-warm the *next*
            // room one step ahead so its lightmaps/meshes are resident, but keep
            // it disabled-but-loaded by toggling only the renderers via the
            // RoomActivator's PreWarm path.
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
        }

        public RoomDescriptor GetDescriptor(MuseumState state)
            => _byState.TryGetValue(state, out var r) ? r : null;
    }
}
