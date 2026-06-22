// -----------------------------------------------------------------------------
//  XRGrabTwistDisk.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A grabbable dial constrained to twist about a single axis. Built for the
//  Caesar cipher disk (and reused by the Enigma rotors). Rather than fight the
//  generic XR Grab Interactable's 6-DoF pose tracking, this is a purpose-built
//  XRBaseInteractable that, while selected, converts the hand's rotation *about
//  the disk axis* into disk spin, snaps to one of N detents (26 for the alphabet),
//  and reports the active step.
//
//  Why a custom interactable: a real museum dial should feel like it's on a
//  spindle — it must ignore hand translation and off-axis rotation, give tactile
//  detents, and never fly off. That is exactly what this does, with a per-detent
//  click and a release snap-tween.
//
//  Interaction style supported: direct grab (controller grip) and — because it
//  derives from XRBaseInteractable — ray-grab if a Ray Interactor with select is
//  used. Twisting is driven by the change in the interactor's orientation around
//  the axis between frames (a stable delta integration, no reference drift).
// -----------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Decrypted.Interaction
{
    [AddComponentMenu("DECRYPTED/XR Grab-Twist Disk")]
    public class XRGrabTwistDisk : XRBaseInteractable
    {
        [Header("Axis & Detents")]
        [Tooltip("Local axis the disk spins around (default = local up).")]
        [SerializeField] private Vector3 _localAxis = Vector3.up;
        [Tooltip("Number of evenly-spaced detents around the dial (26 for A-Z).")]
        [SerializeField] private int _detents = 26;
        [Tooltip("Invert the spin direction if the model reads the wrong way.")]
        [SerializeField] private bool _invert = false;

        [Header("Feel")]
        [Tooltip("Snap-to-detent time on release (s).")]
        [SerializeField] private float _snapSeconds = 0.12f;
        [Tooltip("Soft detent magnetism while turning (0 = free, 1 = sticky).")]
        [Range(0f, 1f)] [SerializeField] private float _detentMagnetism = 0.25f;

        [Header("Audio")]
        [SerializeField] private string _detentSfxKey = "sfx_brass_click";
        [SerializeField] private float _detentVolume = 0.7f;

        [Header("Animation (procedural by default; Blender = Animator)")]
        [Tooltip("When true, drive a Unity Animator (Blender FBX) via the 'DiskAngle' " +
                 "float instead of rotating the transform directly. Default false = " +
                 "current behaviour. See Documentation/08_Animation_Pipeline.md.")]
        [SerializeField] private bool _useBlenderAnimations = false;
        [SerializeField] private Animator _blenderAnimator;

        /// <summary>Current detent index in [0, Detents-1].</summary>
        public int CurrentStep { get; private set; }

        /// <summary>Raised whenever the active detent changes (passes the new step).</summary>
        public event Action<int> OnStepChanged;

        private float _angle;            // accumulated spin in degrees (continuous)
        private int _lastReportedStep = -1;
        private Vector3 _prevDir;        // interactor direction on the spin plane
        private bool _hasPrev;
        private Coroutine _snap;
        private float StepDeg => 360f / Mathf.Max(1, _detents);

        // All visible rotation of the dial goes through this swappable animator, so
        // the interaction/puzzle logic below never moves a transform directly.
        private IDiskAnimator _diskAnim;
        private IDiskAnimator Anim => _diskAnim ??= (_useBlenderAnimations && _blenderAnimator != null)
            ? (IDiskAnimator)new BlenderDiskAnimator(_blenderAnimator)
            : new ProceduralDiskAnimator(transform, _localAxis);

        // ----------------------------------------------------------- selection

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            _hasPrev = false; // reset delta integration for this grab
            if (_snap != null) { StopCoroutine(_snap); _snap = null; }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            // Snap to the nearest detent for a crisp, satisfying lock.
            _snap = StartCoroutine(SnapToNearest());
        }

        // --------------------------------------------------------- per-frame XRI

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
            if (!isSelected || interactorsSelecting.Count == 0) return;

            var interactor = interactorsSelecting[0];
            Transform attach = interactor.GetAttachTransform(this);
            if (attach == null) return;

            Vector3 axis = transform.TransformDirection(_localAxis).normalized;

            // Direction from the spindle to the hand, flattened onto the spin plane.
            Vector3 dir = Vector3.ProjectOnPlane(attach.position - transform.position, axis);
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();

            if (_hasPrev)
            {
                float delta = Vector3.SignedAngle(_prevDir, dir, axis);
                if (_invert) delta = -delta;
                _angle += delta;
                ApplyRotation();
                ReportStep();
            }

            _prevDir = dir;
            _hasPrev = true;
        }

        // -------------------------------------------------------------- helpers

        private void ApplyRotation()
        {
            // Optional soft magnetism nudges the visible angle toward the nearest
            // detent so turning feels mechanical without hard-locking the hand.
            float visible = _angle;
            if (_detentMagnetism > 0f)
            {
                float nearest = Mathf.Round(_angle / StepDeg) * StepDeg;
                visible = Mathf.Lerp(_angle, nearest, _detentMagnetism);
            }
            Anim.ApplyAngle(visible);
        }

        private void ReportStep()
        {
            int step = Mathf.RoundToInt(_angle / StepDeg);
            step = ((step % _detents) + _detents) % _detents; // wrap into [0, detents)
            if (step != _lastReportedStep)
            {
                _lastReportedStep = step;
                CurrentStep = step;
                if (!string.IsNullOrEmpty(_detentSfxKey) && Managers.AudioManager.Instance != null)
                    Managers.AudioManager.Instance.Play(_detentSfxKey, transform.position, true, _detentVolume);
                OnStepChanged?.Invoke(step);
            }
        }

        private System.Collections.IEnumerator SnapToNearest()
        {
            float target = Mathf.Round(_angle / StepDeg) * StepDeg;
            float start = _angle;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _snapSeconds);
                _angle = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                Anim.ApplyAngle(_angle);
                yield return null;
            }
            _angle = target;
            Anim.ApplyAngle(_angle);
            ReportStep();
            _snap = null;
        }

        /// <summary>Programmatically set the dial (used by reset / demo mode).</summary>
        public void SetStep(int step, bool silent = false)
        {
            step = ((step % _detents) + _detents) % _detents;
            _angle = step * StepDeg;
            Anim.ApplyAngle(_angle);
            if (silent) { _lastReportedStep = step; CurrentStep = step; }
            else ReportStep();
        }

        /// <summary>Smoothly rotate the dial to a step (used when the Enigma rotors
        /// visibly advance as the player types). Reports the new step on arrival.</summary>
        public void AnimateToStep(int step, float seconds = 0.1f)
        {
            if (_snap != null) StopCoroutine(_snap);
            _snap = StartCoroutine(AnimateRoutine(step, seconds));
        }

        private System.Collections.IEnumerator AnimateRoutine(int step, float seconds)
        {
            step = ((step % _detents) + _detents) % _detents;
            // Choose the shortest signed delta to the target detent.
            float target = step * StepDeg;
            float start = _angle;
            float delta = Mathf.DeltaAngle(start % 360f, target % 360f);
            float end = start + delta;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, seconds);
                _angle = Mathf.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                Anim.ApplyAngle(_angle);
                yield return null;
            }
            _angle = target;
            Anim.ApplyAngle(_angle);
            ReportStep();
            _snap = null;
        }
    }
}
