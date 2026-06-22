// -----------------------------------------------------------------------------
//  EnigmaLeverPull.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 2)
//
//  The big brass commit lever on the side of the Enigma machine. Once the player
//  has dialled the rotors to the key and typed the ciphertext (so the lamps have
//  spelled the plaintext), they grab this lever and pull it down to "commit" the
//  decryption — the dramatic, physical beat that powers the machine up.
//
//  Like XRGrabTwistDisk this is a purpose-built XRBaseInteractable rather than a
//  generic grab, because a lever should pivot on its hinge and ignore hand
//  translation / off-axis motion. While held, the hand's rotation *about the
//  hinge axis* drives the lever angle (stable delta-integration, no reference
//  drift). Past a pull threshold it fires OnPulled exactly once; on release it
//  eases back to rest, unless it has latched (stays down once committed).
//
//  It deliberately reuses the same interaction grammar as the dials so the whole
//  machine feels mechanically consistent in the hand.
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using Decrypted.Managers;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Decrypted.Interaction
{
    [AddComponentMenu("DECRYPTED/Enigma Lever Pull")]
    public class EnigmaLeverPull : XRBaseInteractable
    {
        [Header("Hinge")]
        [Tooltip("Local axis the lever pivots around (default = local X / pitch).")]
        [SerializeField] private Vector3 _hingeAxis = Vector3.right;
        [Tooltip("Lever angle at rest (degrees about the hinge axis).")]
        [SerializeField] private float _restAngle = 0f;
        [Tooltip("Lever angle fully pulled (degrees). Sign sets the pull direction.")]
        [SerializeField] private float _pulledAngle = -70f;
        [Tooltip("Fraction of the travel (0..1) that counts as a committed pull.")]
        [Range(0.4f, 1f)] [SerializeField] private float _pullThreshold = 0.85f;

        [Header("Feel")]
        [Tooltip("Seconds for the lever to spring back to rest on release.")]
        [SerializeField] private float _returnSeconds = 0.25f;
        [Tooltip("If true the lever stays down after a successful pull (committed).")]
        [SerializeField] private bool _latchWhenPulled = true;

        [Header("Audio")]
        [SerializeField] private string _pullSfxKey = "sfx_lever_pull";
        [SerializeField] private float _pullVolume = 1f;

        /// <summary>Raised once when the lever crosses the pull threshold.</summary>
        public event Action OnPulled;

        /// <summary>True once the lever has been pulled and latched.</summary>
        public bool IsLatched { get; private set; }

        /// <summary>Gate set by the controller: the lever only commits when armed.</summary>
        public bool Armed { get; set; } = true;

        private float _angle;            // current lever angle about the hinge
        private Vector3 _prevDir;        // interactor direction on the hinge plane
        private bool _hasPrev;
        private bool _firedThisPull;
        private Coroutine _return;

        protected override void Awake()
        {
            base.Awake();
            _angle = _restAngle;
            ApplyAngle();
        }

        // ----------------------------------------------------------- selection

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            _hasPrev = false;
            _firedThisPull = false;
            if (_return != null) { StopCoroutine(_return); _return = null; }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            // Spring back unless we latched on a successful commit.
            if (!IsLatched)
                _return = StartCoroutine(ReturnToRest());
        }

        // --------------------------------------------------------- per-frame XRI

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
            if (!isSelected || interactorsSelecting.Count == 0) return;
            if (IsLatched) return;

            var interactor = interactorsSelecting[0];
            Transform attach = interactor.GetAttachTransform(this);
            if (attach == null) return;

            Vector3 axis = transform.TransformDirection(_hingeAxis).normalized;
            Vector3 dir = Vector3.ProjectOnPlane(attach.position - transform.position, axis);
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();

            if (_hasPrev)
            {
                float delta = Vector3.SignedAngle(_prevDir, dir, axis);
                _angle = ClampToTravel(_angle + delta);
                ApplyAngle();
                EvaluatePull();
            }

            _prevDir = dir;
            _hasPrev = true;
        }

        // -------------------------------------------------------------- helpers

        /// <summary>Normalised pull amount in [0,1] from rest to fully pulled.</summary>
        public float Normalised =>
            Mathf.Clamp01(Mathf.InverseLerp(_restAngle, _pulledAngle, _angle));

        /// <summary>Programmatic pull (used by the demo director / auto-play).</summary>
        public void ForcePull()
        {
            if (IsLatched) return;
            _angle = _pulledAngle;
            ApplyAngle();
            EvaluatePull();
        }

        /// <summary>Reset the lever to rest and clear the latch (used by full reset).</summary>
        public void ResetLever()
        {
            if (_return != null) { StopCoroutine(_return); _return = null; }
            IsLatched = false;
            _firedThisPull = false;
            _angle = _restAngle;
            ApplyAngle();
        }

        private void EvaluatePull()
        {
            if (_firedThisPull || !Armed) return;
            if (Normalised >= _pullThreshold)
            {
                _firedThisPull = true;
                if (_latchWhenPulled) { IsLatched = true; _angle = _pulledAngle; ApplyAngle(); }

                if (!string.IsNullOrEmpty(_pullSfxKey) && AudioManager.Instance != null)
                    AudioManager.Instance.Play(_pullSfxKey, transform.position, true, _pullVolume);

                OnPulled?.Invoke();
            }
        }

        private float ClampToTravel(float angle)
        {
            // Clamp between rest and pulled regardless of which way the sign goes.
            float lo = Mathf.Min(_restAngle, _pulledAngle);
            float hi = Mathf.Max(_restAngle, _pulledAngle);
            return Mathf.Clamp(angle, lo, hi);
        }

        private IEnumerator ReturnToRest()
        {
            float start = _angle;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _returnSeconds);
                _angle = Mathf.Lerp(start, _restAngle, Mathf.SmoothStep(0f, 1f, t));
                ApplyAngle();
                yield return null;
            }
            _angle = _restAngle;
            ApplyAngle();
            _return = null;
        }

        private void ApplyAngle()
        {
            transform.localRotation = Quaternion.AngleAxis(_angle, _hingeAxis.normalized);
        }
    }
}
