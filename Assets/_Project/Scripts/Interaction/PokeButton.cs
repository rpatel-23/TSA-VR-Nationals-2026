// -----------------------------------------------------------------------------
//  PokeButton.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A reusable physical button that responds to BOTH a finger poke and a ray
//  "click". We derive from XRSimpleInteractable so that, under XR Interaction
//  Toolkit 2.5.x, a Poke Interactor and a Ray Interactor both raise the standard
//  select events — giving us one code path for two interaction styles. This is
//  the backbone for the splash PLAY button, the Enigma keyboard keys and the
//  vault keypad.
//
//  Behaviour:
//   * On press: depress the cap by a configurable travel, play a click SFX,
//     pulse an emissive highlight, fire UnityEvent + C# Action.
//   * Debounced so a single poke doesn't double-fire.
//
//  Designers wire everything in the Inspector. Code consumers subscribe to the
//  OnPressed Action (preferred for the keyboards, which need the key value).
// -----------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace Decrypted.Interaction
{
    [AddComponentMenu("DECRYPTED/Poke Button")]
    public class PokeButton : XRSimpleInteractable
    {
        [Header("Press Feedback")]
        [Tooltip("Transform of the moving cap. Depressed along its local -Y by Travel.")]
        [SerializeField] private Transform _cap;
        [Tooltip("How far (m) the cap sinks when pressed.")]
        [SerializeField] private float _travel = 0.006f;
        [Tooltip("Seconds for the cap to sink and spring back.")]
        [SerializeField] private float _pressSeconds = 0.06f;

        [Header("Highlight")]
        [Tooltip("Renderer whose emission pulses on press (optional).")]
        [SerializeField] private Renderer _highlightRenderer;
        [SerializeField] private Color _highlightColor = new Color(0.2f, 1f, 1f, 1f);
        [SerializeField] private float _highlightSeconds = 0.18f;
        [Tooltip("Steady glow (0-1) shown while a hand or ray is focused on this button, so " +
                 "the player can see EXACTLY which key they're about to press.")]
        [SerializeField] private float _hoverGlow = 0.5f;

        [Header("Audio")]
        [Tooltip("AudioManager key for the click. Empty = silent.")]
        [SerializeField] private string _clickSfxKey = "sfx_brass_click";

        [Header("Logic")]
        [Tooltip("Re-press lockout to debounce a single poke.")]
        [SerializeField] private float _debounce = 0.12f;
        [Tooltip("Optional identifier passed to OnPressed (e.g. the key letter).")]
        [SerializeField] private string _value = "";

        [Header("Events")]
        public UnityEvent Pressed;

        /// <summary>Fired with this button's Value string (letter, command, …).</summary>
        public event Action<string> OnPressed;

        public string Value { get => _value; set => _value = value; }

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private Vector3 _capHome;
        private float _lastPress = -999f;
        private Coroutine _anim;
        private bool _hovered;

        protected override void Awake()
        {
            base.Awake();
            _mpb = new MaterialPropertyBlock();
            if (_cap != null) _capHome = _cap.localPosition;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            TryPress();
        }

        // Focus glow: while a hand/poke/ray is hovering this key, keep it lit so the player
        // knows precisely which key the press will land on (prevents the "big hand hitbox
        // pressed the wrong key" confusion).
        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);
            _hovered = true;
            if (_anim == null) SetHighlight(_hoverGlow);
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);
            _hovered = false;
            if (_anim == null) SetHighlight(0f);
        }

        /// <summary>Public so a DemoDirector or test harness can press it directly.</summary>
        public void TryPress()
        {
            if (Time.time - _lastPress < _debounce) return;
            _lastPress = Time.time;

            // Audio — spatialised at the button so it localises in the headset.
            if (!string.IsNullOrEmpty(_clickSfxKey) && Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.Play(_clickSfxKey, transform.position, true, 0.9f);

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(PressRoutine());

            Pressed?.Invoke();
            OnPressed?.Invoke(_value);
        }

        private System.Collections.IEnumerator PressRoutine()
        {
            // Sink.
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _pressSeconds);
                if (_cap != null) _cap.localPosition = _capHome + Vector3.down * (_travel * Mathf.SmoothStep(0f, 1f, t));
                SetHighlight(Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            // Spring back.
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _pressSeconds);
                if (_cap != null) _cap.localPosition = _capHome + Vector3.down * (_travel * (1f - Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            if (_cap != null) _cap.localPosition = _capHome;

            // Fade back down to the hover level if still focused, otherwise off.
            float endGlow = _hovered ? _hoverGlow : 0f;
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _highlightSeconds);
                SetHighlight(Mathf.Lerp(1f, endGlow, t));
                yield return null;
            }
            SetHighlight(endGlow);
        }

        private void SetHighlight(float k)
        {
            if (_highlightRenderer == null) return;
            _highlightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorID, _highlightColor * Mathf.Clamp01(k));
            _highlightRenderer.SetPropertyBlock(_mpb);
        }
    }
}
