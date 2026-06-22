// -----------------------------------------------------------------------------
//  VRHandPoser.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  Drives a stylized low-poly VR hand: the hand mesh is a pure positional/
//  rotational puppet of the controller (it is parented to the controller, so pose
//  tracking is automatic) and the only animation is a simple two-pose finger curl
//  driven by the grip and trigger inputs:
//
//    * Grip squeezed  -> all fingers curl toward the palm (make a fist).
//    * Trigger pulled -> the index finger extends (pointing), the rest follow grip.
//    * Thumb curls partially with the grip.
//
//  Both calibration guesses are exposed and previewable so they can be corrected
//  in one click after a headset check, WITHOUT recompiling:
//    * Rest orientation (palms inward) lives in _restEuler (flip Z to mirror).
//    * Curl direction lives in _curlSign (flip if fingers bend the wrong way).
//  An edit-mode preview slider (_previewCurl) and two context-menu "Flip" commands
//  make verification trivial. Nothing is hardcoded.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.InputSystem;

namespace Decrypted.Interaction
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class VRHandPoser : MonoBehaviour
    {
        [Header("Inputs (XRI Default Input Actions)")]
        [Tooltip("Grip squeeze value 0..1 (XRI <Hand> Interaction/Select Value).")]
        [SerializeField] private InputActionReference _gripValue;
        [Tooltip("Trigger value 0..1 (XRI <Hand> Interaction/Activate Value).")]
        [SerializeField] private InputActionReference _triggerValue;

        [Header("Rest orientation (palms inward) - FLIPPABLE")]
        [Tooltip("Fixed rotation offset of the hand relative to the controller grip " +
                 "space. Left hand is typically (0,0,90), right (0,0,-90). Flip the Z " +
                 "sign (or use the 'Flip Palm Roll' context menu) if the palm faces " +
                 "the wrong way after a headset check.")]
        [SerializeField] private Vector3 _restEuler = new Vector3(0f, 0f, 90f);
        [Tooltip("Apply the rest orientation to this transform.")]
        [SerializeField] private bool _applyRestEuler = true;

        [Header("Finger joints (proximal -> distal per finger)")]
        [SerializeField] private Transform[] _indexJoints;
        [SerializeField] private Transform[] _middleRingPinkyJoints;
        [SerializeField] private Transform[] _thumbJoints;

        [Header("Curl (degrees per joint)")]
        [SerializeField] private float _openPerJoint = 6f;
        [SerializeField] private float _closedPerJoint = 28f;
        [Tooltip("Curl axis sign. Flip (or use the 'Flip Curl Direction' context " +
                 "menu) if fingers bend backward.")]
        [SerializeField] private float _curlSign = 1f;
        [SerializeField] private float _lerpSpeed = 18f;

        [Header("Editor preview (no effect at runtime)")]
        [Tooltip("Preview the finger curl in edit mode (0 = open, 1 = fist) to verify " +
                 "the curl direction without entering play or wearing the headset.")]
        [Range(0f, 1f)] [SerializeField] private float _previewCurl = 0f;

        private float _curl, _index, _thumb;

        // ----- one-click calibration after a headset check -----
        [ContextMenu("Flip Palm Roll")]
        private void FlipPalmRoll() { _restEuler.z = -_restEuler.z; ApplyRestEuler(); }

        [ContextMenu("Flip Curl Direction")]
        private void FlipCurlDirection() { _curlSign = -_curlSign; ApplyPreview(); }

        private void ApplyRestEuler()
        {
            if (_applyRestEuler) transform.localRotation = Quaternion.Euler(_restEuler);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyRestEuler();
            if (!Application.isPlaying) ApplyPreview();   // live curl-direction preview
        }
#endif

        private void Awake() => ApplyRestEuler();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (_gripValue != null && _gripValue.action != null) _gripValue.action.Enable();
            if (_triggerValue != null && _triggerValue.action != null) _triggerValue.action.Enable();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;   // edit mode uses the preview slider only

            float grip = (_gripValue != null && _gripValue.action != null) ? _gripValue.action.ReadValue<float>() : 0f;
            float trig = (_triggerValue != null && _triggerValue.action != null) ? _triggerValue.action.ReadValue<float>() : 0f;

            float targetCurl = grip;                        // middle/ring/pinky follow grip
            float targetIndex = Mathf.Lerp(grip, 0f, trig); // trigger extends index (pointing)
            float targetThumb = grip * 0.7f;

            float k = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime); // frame-rate independent ease
            _curl = Mathf.Lerp(_curl, targetCurl, k);
            _index = Mathf.Lerp(_index, targetIndex, k);
            _thumb = Mathf.Lerp(_thumb, targetThumb, k);

            Apply(_middleRingPinkyJoints, _curl);
            Apply(_indexJoints, _index);
            Apply(_thumbJoints, _thumb);
        }

        // Edit-mode: drive every finger to the preview amount so the curl direction
        // is visible without input.
        private void ApplyPreview()
        {
            Apply(_middleRingPinkyJoints, _previewCurl);
            Apply(_indexJoints, _previewCurl);
            Apply(_thumbJoints, _previewCurl * 0.7f);
        }

        private void Apply(Transform[] joints, float amount)
        {
            if (joints == null) return;
            float a = Mathf.Lerp(_openPerJoint, _closedPerJoint, Mathf.Clamp01(amount)) * _curlSign;
            for (int i = 0; i < joints.Length; i++)
                if (joints[i] != null) joints[i].localRotation = Quaternion.Euler(a, 0f, 0f);
        }
    }
}
