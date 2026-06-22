// -----------------------------------------------------------------------------
//  UIManager.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Owns transient, non-diegetic UI:
//    * A head-locked-but-comfortable "hint toast" that fades in/out for nudges
//      and feedback (driven by ShowHintEvent).
//    * A persistent wayfinding label ("You are in: …") updated on room entry.
//  Diegetic, in-world plaques are handled by PlaqueController (they live in the
//  scene as physical objects); this manager is only the floating overlay layer.
//
//  VR comfort: the toast is parented to a "comfort anchor" that follows the HMD
//  with damped yaw + a fixed forward distance, so text never clips the player's
//  face and never induces nausea by being hard-locked.
// -----------------------------------------------------------------------------

using System.Collections;
using Decrypted.Core;
using Decrypted.Util;
using TMPro;
using UnityEngine;

namespace Decrypted.Managers
{
    [DefaultExecutionOrder(-70)]
    public class UIManager : Singleton<UIManager>
    {
        [Header("References")]
        [SerializeField] private Transform _hmd;                 // main camera transform
        [SerializeField] private CanvasGroup _toastGroup;        // alpha-controlled toast
        [SerializeField] private TMP_Text _toastLabel;
        [SerializeField] private TMP_Text _wayfindingLabel;      // "You are in: …"

        [Header("Comfort Follow")]
        [Tooltip("How far in front of the HMD the toast sits (metres).")]
        [SerializeField] private float _followDistance = 1.6f;
        [Tooltip("How far below eye-line (metres) so it doesn't block the exhibit.")]
        [SerializeField] private float _followDrop = 0.45f;
        [Tooltip("Damping for the comfort follow (lower = lazier, comfier).")]
        [SerializeField] private float _followLerp = 2.5f;

        [Header("Style")]
        [SerializeField] private float _fadeTime = 0.35f;

        private Coroutine _toastRoutine;
        private Vector3 _toastVel;

        protected override void OnSingletonAwake()
        {
            if (_toastGroup != null) _toastGroup.alpha = 0f;
            if (_hmd == null && Camera.main != null) _hmd = Camera.main.transform;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ShowHintEvent>(OnShowHint);
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ShowHintEvent>(OnShowHint);
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        private void LateUpdate()
        {
            if (_toastGroup == null || _hmd == null) return;

            // Target position: in front and slightly below the gaze, ignoring pitch
            // so the toast doesn't bob when the player looks up/down.
            Vector3 flatFwd = _hmd.forward; flatFwd.y = 0f; flatFwd.Normalize();
            Vector3 target = _hmd.position + flatFwd * _followDistance + Vector3.down * _followDrop;

            var t = _toastGroup.transform;
            t.position = Vector3.SmoothDamp(t.position, target, ref _toastVel, 1f / Mathf.Max(0.01f, _followLerp));
            // Face the player (billboard) with no roll.
            Vector3 look = t.position - _hmd.position; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        // ----------------------------------------------------------- toast

        private void OnShowHint(ShowHintEvent e)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(e.Message, e.Duration));
        }

        private IEnumerator ToastRoutine(string message, float hold)
        {
            if (_toastLabel != null) _toastLabel.text = message;
            yield return Fade(_toastGroup, 0f, 1f, _fadeTime);
            yield return new WaitForSecondsRealtime(hold); // unscaled: survives a time freeze
            yield return Fade(_toastGroup, 1f, 0f, _fadeTime);
            _toastRoutine = null;
        }

        private static IEnumerator Fade(CanvasGroup g, float from, float to, float time)
        {
            if (g == null) yield break;
            float t = 0f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime; // unscaled: survives a time freeze
                g.alpha = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            g.alpha = to;
        }

        // ------------------------------------------------------- wayfinding

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            if (_wayfindingLabel == null) return;
            var sc = FindObjectOfType<SceneController>();
            var desc = sc != null ? sc.GetDescriptor(e.Room) : null;
            _wayfindingLabel.text = desc != null ? desc.displayName : e.Room.ToString();
        }
    }
}
