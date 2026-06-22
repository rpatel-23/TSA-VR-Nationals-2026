// -----------------------------------------------------------------------------
//  VaultController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 3)
//
//  Owns the payoff when the vault keypad accepts the passphrase: the heavy door
//  unlocks and swings/slides open, the locking ring spins, status lights flip
//  from red to green, the room's lighting warms as the secured archive beyond is
//  revealed, and a low rumble crescendos under it all. When the sequence finishes
//  it marks the room solved so the GameManager can carry the player onward.
//
//  Two door styles are supported with zero code change for the designer:
//   * Hinged: a pivot transform is rotated open about its local up axis.
//   * Sliding: the door transform translates along a local offset.
//  An Animator may also be used instead (trigger "Open"); if present it wins.
//
//  All emissive changes use MaterialPropertyBlocks (no material instances).
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Managers;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class VaultController : MonoBehaviour
    {
        public enum DoorStyle { Hinged, Sliding }

        [Header("Door")]
        [SerializeField] private DoorStyle _style = DoorStyle.Hinged;
        [Tooltip("The door object that moves. For Hinged, this should pivot at its hinge.")]
        [SerializeField] private Transform _door;
        [Tooltip("Optional EDGE hinge: an empty parent placed at the door's EDGE with the " +
                 "slab parented under it. If set, the swing rotates THIS, so the door opens " +
                 "from its edge like a real door instead of spinning about its own centre. " +
                 "Leave null to rotate the door transform directly. Hinged style only.")]
        [SerializeField] private Transform _hingePivot;
        [Tooltip("Optional Animator; if set, its 'Open' trigger is fired and the " +
                 "transform tween below is skipped.")]
        [SerializeField] private Animator _doorAnimator;
        [Tooltip("Hinged: open angle (deg) about local Y (the hinge axis). Sliding: ignored.")]
        [SerializeField] private float _openAngle = 100f;
        [Tooltip("Sliding: local offset to the open position. Hinged: ignored.")]
        [SerializeField] private Vector3 _openOffset = new Vector3(0f, 0f, 2.4f);
        [SerializeField] private float _openSeconds = 2.0f;
        [SerializeField] private AnimationCurve _openEase =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("When true, drive the door + ring via a Blender FBX Animator (the " +
                 "'Open' trigger) instead of the procedural tween. Default false = " +
                 "current behaviour. See Documentation/08_Animation_Pipeline.md.")]
        [SerializeField] private bool _useBlenderAnimations = false;

        [Header("Locking ring")]
        [Tooltip("Optional ring that spins as the bolts retract.")]
        [SerializeField] private Transform _lockingRing;
        [SerializeField] private float _ringSpinDegrees = 220f;
        [SerializeField] private float _ringSpinSeconds = 1.0f;

        [Header("Status lights")]
        [SerializeField] private Renderer[] _statusLights;
        [SerializeField] private Color _lockedColor = new Color(1f, 0.18f, 0.15f, 1f);
        [SerializeField] private Color _unlockedColor = new Color(0.3f, 1f, 0.45f, 1f);
        [SerializeField] private float _statusIntensity = 1.5f;

        [Header("Room lighting reveal")]
        [Tooltip("Lights that warm up as the archive is revealed.")]
        [SerializeField] private Light[] _revealLights;
        [SerializeField] private float _revealLightTarget = 1.2f;
        [Tooltip("Emissive panels inside the vault that come alive on open.")]
        [SerializeField] private Renderer[] _archiveEmissive;
        [SerializeField] private Color _archiveColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private float _archivePeak = 1.3f;

        [Header("Audio")]
        [SerializeField] private string _rumbleKey = "sfx_vault_rumble";
        [SerializeField] private string _successKey = "sfx_success_chime";

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private bool _unlocked;
        private Quaternion _doorClosedRot;
        private Vector3 _doorClosedPos;

        // Door + locking-ring motion goes through this swappable animator (procedural
        // tween or Blender Animator), so this controller never moves those transforms
        // directly.
        private IVaultAnimator _vaultAnim;
        private IVaultAnimator VaultAnim => _vaultAnim ??= (_useBlenderAnimations && _doorAnimator != null)
            ? (IVaultAnimator)new BlenderVaultAnimator(_doorAnimator)
            : new ProceduralVaultAnimator(_door, _lockingRing);

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_door != null)
            {
                _doorClosedRot = _door.localRotation;
                _doorClosedPos = _door.localPosition;
            }
            SetStatusLights(_lockedColor);
            SetArchiveEmissive(0f);
            SetRevealLights(0f);
        }

        /// <summary>Called by the keypad on a correct passphrase.</summary>
        public void Unlock()
        {
            if (_unlocked) return;
            _unlocked = true;
            StartCoroutine(UnlockSequence());
        }

        /// <summary>Re-lock everything (used by a full reset / demo loop).</summary>
        public void Relock()
        {
            StopAllCoroutines();
            _unlocked = false;
            VaultAnim.SetDoorRotation(_doorClosedRot);
            VaultAnim.SetDoorPosition(_doorClosedPos);
            SetStatusLights(_lockedColor);
            SetArchiveEmissive(0f);
            SetRevealLights(0f);
        }

        private IEnumerator UnlockSequence()
        {
            // 1) Status flips green + rumble begins.
            SetStatusLights(_unlockedColor);
            if (!string.IsNullOrEmpty(_rumbleKey) && AudioManager.Instance != null)
                AudioManager.Instance.Play(_rumbleKey, transform.position, true, 1f);
            if (!string.IsNullOrEmpty(_successKey) && AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(_successKey, 0.8f);

            // 2) Spin the locking ring as the bolts retract.
            if (_lockingRing != null) yield return SpinRing();

            // 3) Open the door (Animator wins if present), warming the room in parallel.
            if (VaultAnim.TryTriggerOpen())
            {
                StartCoroutine(RevealRoom(_openSeconds));
                yield return new WaitForSecondsRealtime(_openSeconds); // unscaled time
            }
            else if (_door != null)
            {
                yield return OpenDoorTween();
            }
            else
            {
                yield return RevealRoom(_openSeconds);
            }

            // 4) Done — hand off to the flow manager.
            GameManager.Instance?.MarkSolved(MuseumState.VaultRoom);
        }

        private IEnumerator SpinRing()
        {
            Quaternion start = _lockingRing.localRotation;
            Quaternion end = start * Quaternion.AngleAxis(_ringSpinDegrees, Vector3.up);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _ringSpinSeconds); // unscaled time
                VaultAnim.SetRingRotation(Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            VaultAnim.SetRingRotation(end);
        }

        private IEnumerator OpenDoorTween()
        {
            StartCoroutine(RevealRoom(_openSeconds));

            // Swing from the EDGE hinge if one is provided (a real-door swing); otherwise
            // rotate the door transform about its own pivot. Either way the swing is about
            // LOCAL Y (the hinge axis), by _openAngle degrees, over _openSeconds, eased
            // in/out with SmoothStep and interpolated with Quaternion.Slerp.
            Transform swing = _hingePivot != null ? _hingePivot : _door;
            Quaternion rotStart = swing.localRotation;
            Quaternion rotEnd = rotStart * Quaternion.AngleAxis(_openAngle, Vector3.up); // local-Y hinge
            Vector3 posStart = _door.localPosition;
            Vector3 posEnd = _doorClosedPos + _openOffset;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _openSeconds); // unscaled time
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));         // ease-in-out
                if (_style == DoorStyle.Hinged)
                {
                    // Rotate the edge hinge directly, or the door via its animator.
                    if (_hingePivot != null) swing.localRotation = Quaternion.Slerp(rotStart, rotEnd, k);
                    else VaultAnim.SetDoorRotation(Quaternion.Slerp(rotStart, rotEnd, k));
                }
                else
                    VaultAnim.SetDoorPosition(Vector3.Lerp(posStart, posEnd, k));
                yield return null;
            }
            if (_style == DoorStyle.Hinged)
            {
                if (_hingePivot != null) swing.localRotation = rotEnd;
                else VaultAnim.SetDoorRotation(rotEnd);
            }
            else VaultAnim.SetDoorPosition(posEnd);
        }

        private IEnumerator RevealRoom(float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds); // unscaled time
                float k = Mathf.SmoothStep(0f, 1f, t);
                SetRevealLights(k * _revealLightTarget);
                SetArchiveEmissive(k * _archivePeak);
                yield return null;
            }
            SetRevealLights(_revealLightTarget);
            SetArchiveEmissive(_archivePeak);
        }

        // ------------------------------------------------------------- visuals

        private void SetStatusLights(Color c)
        {
            if (_statusLights == null) return;
            foreach (var r in _statusLights)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorID, c * _statusIntensity);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void SetArchiveEmissive(float intensity)
        {
            if (_archiveEmissive == null) return;
            foreach (var r in _archiveEmissive)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorID, _archiveColor * Mathf.Max(0f, intensity));
                r.SetPropertyBlock(_mpb);
            }
        }

        private void SetRevealLights(float intensity)
        {
            if (_revealLights == null) return;
            foreach (var l in _revealLights) if (l != null) l.intensity = Mathf.Max(0f, intensity);
        }
    }
}
