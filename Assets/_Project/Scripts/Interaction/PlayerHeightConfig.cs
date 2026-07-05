// -----------------------------------------------------------------------------
//  PlayerHeightConfig.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  One place to dial in how tall the player feels, using the CORRECT lever for the
//  XR Origin's configured Tracking Origin Mode (the two modes need different fixes):
//
//   * DEVICE mode - the XR Origin's "Camera Y Offset" sets how high the camera sits
//     above the rig floor. We push the serialized _cameraYOffset into
//     XROrigin.CameraYOffset on Start. Lower the value to lower the player.
//
//   * FLOOR mode - the Quest reports the player's real-world headset height, so
//     Camera Y Offset has NO effect; Unity clears the Camera Offset height. To lower
//     the perceived in-world height we set the Camera Offset child's local Y to the
//     serialized _floorHeightOffset (negative = lower). Because the XR runtime
//     re-clears that height when the floor origin is (re)established a frame or two
//     after Start, we re-assert on the tracking-origin-updated event so the trim
//     actually sticks.
//
//  Attach to the XR Origin GameObject. Nothing here is hardcoded - both values are
//  serialized and tunable in the Inspector without recompiling.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class PlayerHeightConfig : MonoBehaviour
    {
        [Tooltip("DEVICE tracking only. Height (metres) of the camera above the rig " +
                 "floor. LOWER this to lower the player. Unity's default is ~1.36; " +
                 "try ~1.1. (No effect in Floor mode.)")]
        [SerializeField] private float _cameraYOffset = 1.1f;

        [Tooltip("FLOOR tracking only. Vertical trim (metres) applied to the Camera " +
                 "Offset child on top of the real tracked height. NEGATIVE lowers the " +
                 "player. Keep this 0 and use Extra Height Boost as the one lever. " +
                 "(No effect in Device mode.)")]
        [SerializeField] private float _floorHeightOffset = 0f;

        [Tooltip("Global vertical raise (metres) added on TOP of the per-mode values above, " +
                 "in BOTH Device and Floor modes. This is the SINGLE clean lever for nudging " +
                 "the whole view up/down: do NOT also raise the XR rig root, the Camera Offset " +
                 "child, or the PlayerAnchors - those either stack into an over-correction or " +
                 "are ignored/overwritten elsewhere. 0 = real headset/floor height (game " +
                 "level); +0.3 = +1 ft up, -0.3 = ~1 ft down (1 ft = ~0.305 m).")]
        [SerializeField] private float _extraHeightBoost = 0.0f;

        // NOTE: the exact value needs ONE headset test to confirm it feels right
        // relative to the museum exhibit surfaces (disk, keyboard, vault keypad).

        private XROrigin _origin;
        private readonly List<XRInputSubsystem> _subsystems = new List<XRInputSubsystem>();
        private bool _subscribed;

        private void Start()
        {
            _origin = GetComponent<XROrigin>();
            if (_origin == null) _origin = GetComponentInParent<XROrigin>();
            Apply();
            SubscribeOriginUpdates();   // Floor mode re-clears on origin events; re-assert
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            foreach (var s in _subsystems)
                if (s != null) s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            _subscribed = false;
        }

        private void Apply()
        {
            if (_origin == null) return;

            // Decide by the CONFIGURED mode (the Inspector field on the XR Origin).
            if (_origin.RequestedTrackingOriginMode == XROrigin.TrackingOriginMode.Device)
            {
                _origin.CameraYOffset = _cameraYOffset + _extraHeightBoost;
            }
            else // Floor (or "Not Specified" defaulting to floor on Quest)
            {
                var off = _origin.CameraFloorOffsetObject;
                if (off != null)
                {
                    var p = off.transform.localPosition;
                    p.y = _floorHeightOffset + _extraHeightBoost; // runtime base in floor mode is 0, so this IS the trim
                    off.transform.localPosition = p;
                }
            }
        }

        private void SubscribeOriginUpdates()
        {
            if (_subscribed) return;
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var s in _subsystems)
                if (s != null) s.trackingOriginUpdated += OnTrackingOriginUpdated;
            _subscribed = _subsystems.Count > 0;
        }

        private void OnTrackingOriginUpdated(XRInputSubsystem subsystem) => Apply();
    }
}
