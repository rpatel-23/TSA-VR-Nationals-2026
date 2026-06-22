// -----------------------------------------------------------------------------
//  ArtifactAnimators.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  Separates the ARTIFACT ANIMATION (where parts physically move) from the
//  INTERACTION / PUZZLE logic. Each interactive exhibit talks to a small swappable
//  interface; the interaction script never touches a transform of an artifact part
//  directly anymore. Two implementations exist for each:
//
//    * Procedural<X>Animator - moves the transforms in C# exactly as the project
//      always has (the default; nothing changes).
//    * Blender<X>Animator - drives a Unity Animator instead, so a Blender-exported
//      FBX with baked keyframes can play the motion. See
//      Documentation/08_Animation_Pipeline.md for the export + parameter names.
//
//  The owning MonoBehaviour picks which one via a [SerializeField] bool
//  _useBlenderAnimations (default false => Procedural => identical behaviour).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace Decrypted.Interaction
{
    // ============================== CAESAR DISK / ENIGMA ROTOR ================
    /// <summary>A dial that visually sits at a continuous angle (degrees) about its
    /// spin axis. Used by the Caesar cipher disk and each Enigma rotor.</summary>
    public interface IDiskAnimator
    {
        void ApplyAngle(float degrees);
    }

    /// <summary>Current behaviour: rotate the transform about its local axis, on top of
    /// the disk's resting pose so it stays flat and only spins.</summary>
    public sealed class ProceduralDiskAnimator : IDiskAnimator
    {
        private readonly Transform _target;
        private readonly Vector3 _axis;
        private readonly Quaternion _rest;   // the disk's flat resting orientation, captured once
        public ProceduralDiskAnimator(Transform target, Vector3 axis)
        {
            _target = target;
            _axis = axis.normalized;
            // Capture the authored REST orientation so a twist only ADDS spin about the
            // constrained axis on top of it - it must never REPLACE the rotation. The old
            // code set localRotation = AngleAxis(deg, axis), which discarded any resting
            // tilt and made a flat disk snap upright ("stand up") the instant it was turned.
            _rest = (target != null) ? target.localRotation : Quaternion.identity;
        }

        // CONSTRAINED AXIS: the disk spins ONLY about its local _axis (the Caesar disk's
        // spin axis is local up / Vector3.up, perpendicular to its face). Composing
        // rest * spin preserves the flat resting pose, so the disk stays horizontal and can
        // never tip, tilt, or stand up, and its position is never touched (not liftable).
        // Do NOT change this back to assigning AngleAxis(deg, axis) directly.
        public void ApplyAngle(float degrees)
        {
            if (_target != null) _target.localRotation = _rest * Quaternion.AngleAxis(degrees, _axis);
        }
    }

    /// <summary>Blender pathway: feed the angle to an Animator float parameter that
    /// drives the dial's rotation clip.</summary>
    public sealed class BlenderDiskAnimator : IDiskAnimator
    {
        private readonly Animator _animator;
        public BlenderDiskAnimator(Animator animator) { _animator = animator; }

        public void ApplyAngle(float degrees)
        {
            // Blender: a single-bone "Dial" rotates 0..360 on local up; export the
            // rotation clip and read this float in the AnimatorController to scrub it.
            if (_animator != null) _animator.SetFloat("DiskAngle", degrees);
        }
    }

    // ===================================== ENIGMA (exit door) =================
    /// <summary>The Enigma's power-up exit door (a sliding part).</summary>
    public interface IEnigmaAnimator
    {
        /// <summary>Returns true if an Animator handled the open (skip the tween).</summary>
        bool TryTriggerOpen();
        void SetExitDoorPosition(Vector3 localPosition);
    }

    public sealed class ProceduralEnigmaAnimator : IEnigmaAnimator
    {
        private readonly Transform _exitDoor;
        public ProceduralEnigmaAnimator(Transform exitDoor) { _exitDoor = exitDoor; }
        public bool TryTriggerOpen() => false;
        public void SetExitDoorPosition(Vector3 p) { if (_exitDoor != null) _exitDoor.localPosition = p; }
    }

    public sealed class BlenderEnigmaAnimator : IEnigmaAnimator
    {
        private readonly Animator _animator;
        public BlenderEnigmaAnimator(Animator animator) { _animator = animator; }
        public bool TryTriggerOpen()
        {
            // Blender: bake the door-open as a clip wired to the "Open" trigger.
            if (_animator == null) return false;
            _animator.SetTrigger("Open");
            return true;
        }
        public void SetExitDoorPosition(Vector3 p) { /* the FBX clip drives the door */ }
    }

    // ===================================== VAULT (door + ring) ================
    /// <summary>The vault's heavy door (hinged rotation or slide) and locking ring.</summary>
    public interface IVaultAnimator
    {
        /// <summary>Returns true if an Animator handled the open (skip the tween).</summary>
        bool TryTriggerOpen();
        void SetDoorRotation(Quaternion localRotation);
        void SetDoorPosition(Vector3 localPosition);
        void SetRingRotation(Quaternion localRotation);
    }

    public sealed class ProceduralVaultAnimator : IVaultAnimator
    {
        private readonly Transform _door, _ring;
        public ProceduralVaultAnimator(Transform door, Transform ring) { _door = door; _ring = ring; }
        public bool TryTriggerOpen() => false;
        public void SetDoorRotation(Quaternion q) { if (_door != null) _door.localRotation = q; }
        public void SetDoorPosition(Vector3 p) { if (_door != null) _door.localPosition = p; }
        public void SetRingRotation(Quaternion q) { if (_ring != null) _ring.localRotation = q; }
    }

    public sealed class BlenderVaultAnimator : IVaultAnimator
    {
        private readonly Animator _animator;
        public BlenderVaultAnimator(Animator animator) { _animator = animator; }
        public bool TryTriggerOpen()
        {
            // Blender: bake door-swing + ring-spin into one clip on the "Open" trigger.
            if (_animator == null) return false;
            _animator.SetTrigger("Open");
            return true;
        }
        public void SetDoorRotation(Quaternion q) { /* FBX clip drives the door */ }
        public void SetDoorPosition(Vector3 p) { /* FBX clip drives the door */ }
        public void SetRingRotation(Quaternion q) { /* FBX clip drives the ring */ }
    }
}
