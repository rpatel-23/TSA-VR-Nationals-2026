// -----------------------------------------------------------------------------
//  XRBodyColliderDriver.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  Keeps the locomotion CharacterController's capsule under the HMD every frame.
//
//  The XR rig carries a CharacterController that the continuous-move provider uses
//  for collision. By default its capsule is anchored at the RIG ORIGIN, not under
//  the headset. So when the player physically (room-scale) walks away from the rig
//  origin and then walks back in, the collision capsule desyncs from where the
//  player's head actually is, and can jam against the room-centre exhibit / plinth
//  / rope-barrier colliders. The result is an invisible wall that "won't let you
//  approach the centre of the room" - exactly the reported bug.
//
//  Unity XRIT ships a CharacterControllerDriver, but it only re-syncs the capsule
//  on locomotion BEGIN/END events (it is built for "resize before thumbstick move"),
//  so it misses pure room-scale walking. This component re-syncs continuously in
//  LateUpdate (after tracking has posed the camera for the frame), so the capsule
//  always follows the head and the desync can never build up.
//
//  Attach to the XR Origin GameObject (the one holding the CharacterController and
//  XROrigin). Heights are clamped and serialized so they are tunable without code.
// -----------------------------------------------------------------------------

using Unity.XR.CoreUtils;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class XRBodyColliderDriver : MonoBehaviour
    {
        [Tooltip("Smallest capsule height (m) this driver will set, regardless of how " +
                 "low the tracked HMD reports (keeps a sane body when crouching/leaning).")]
        [SerializeField] private float _minHeight = 0.9f;

        [Tooltip("Largest capsule height (m) this driver will set.")]
        [SerializeField] private float _maxHeight = 2.2f;

        [Tooltip("When true the capsule blocks locomotion against world colliders. " +
                 "Turn OFF to make the body purely cosmetic (never blocks the player) " +
                 "if collision ever causes trouble in the auto-toured showcase.")]
        [SerializeField] private bool _collide = true;

        private XROrigin _origin;
        private CharacterController _cc;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _origin = GetComponent<XROrigin>();
            if (_origin == null) _origin = GetComponentInParent<XROrigin>();
        }

        // LateUpdate so the camera has already been posed by the XR tracking this frame.
        private void LateUpdate()
        {
            if (_cc == null || _origin == null) return;

            float height = Mathf.Clamp(_origin.CameraInOriginSpaceHeight, _minHeight, _maxHeight);

            // Camera position in origin (rig) space → the capsule's X/Z follow the head.
            Vector3 center = _origin.CameraInOriginSpacePos;
            center.y = height * 0.5f + _cc.skinWidth;

            _cc.height = height;
            _cc.center = center;
            if (_cc.detectCollisions != _collide) _cc.detectCollisions = _collide;
        }
    }
}
