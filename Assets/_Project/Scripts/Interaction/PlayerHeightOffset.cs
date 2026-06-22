// -----------------------------------------------------------------------------
//  PlayerHeightOffset.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A small, Inspector-tunable vertical trim on the XR rig's Camera Offset. The rig
//  runs in local-floor tracking, where XROrigin's Camera Y Offset is ignored and
//  the camera sits at the player's real standing height. This component applies a
//  serialized offset on TOP of whatever the tracking origin sets, so the player can
//  be lowered (or raised) without recompiling and regardless of tracking mode.
//
//  It captures the tracking-managed base height once (after XROrigin has set it),
//  then each LateUpdate pins the Camera Offset to base + _heightOffset. Lower the
//  value to bring the viewpoint down. The exact amount needs one headset check.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace Decrypted.Interaction
{
    [DefaultExecutionOrder(1000)]   // run after XROrigin has positioned the offset
    [DisallowMultipleComponent]
    public class PlayerHeightOffset : MonoBehaviour
    {
        [Tooltip("The XR Origin's Camera Offset transform (parent of the HMD camera).")]
        [SerializeField] private Transform _cameraOffset;

        [Tooltip("Vertical trim in metres applied on top of XR tracking. Negative " +
                 "lowers the player. Start around -0.18; confirm the feel in headset.")]
        [SerializeField] private float _heightOffset = -0.18f;

        private float _baseY;
        private bool _captured;

        private void OnDisable() => _captured = false;   // re-capture if re-enabled

        private void LateUpdate()
        {
            if (_cameraOffset == null) return;
            if (!_captured) { _baseY = _cameraOffset.localPosition.y; _captured = true; }
            var p = _cameraOffset.localPosition;
            p.y = _baseY + _heightOffset;
            _cameraOffset.localPosition = p;
        }
    }
}
