// -----------------------------------------------------------------------------
//  RoomButton.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A professional, physical piston-style VR button (the Splash "PRESS TO BEGIN").
//  Built procedurally from a wide dark cylindrical housing and a raised accent
//  press-cap, with a floating SDF label above it. It is pressed by DIRECT physical
//  contact (the hand's collision volume entering the button's trigger - no rays,
//  consistent with the rest of the hand system) and gives full multimodal feedback:
//
//    * the cap pistons down 0.015 m over 0.08 s, then springs back over 0.12 s,
//    * a clean ui_confirm click plays through the AudioManager,
//    * a 0.4 / 0.1 s haptic pulse fires on BOTH controllers at the moment of contact,
//    * the cap emissive brightens on hover and brighter on press, easing back.
//
//  On press it raises ExperienceStartedEvent (Splash -> Atrium). Placement is a
//  serialized field so it can be dialled in from the Inspector.
// -----------------------------------------------------------------------------

using System.Collections;
using Decrypted.Core;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class RoomButton : MonoBehaviour
    {
        [Header("Placement (local to parent; ~0.9-1.0 m up, ~0.6 m in front of spawn)")]
        [SerializeField] private Vector3 _localPosition = new Vector3(0f, 0.95f, 0.6f);

        [Header("Press feel")]
        [SerializeField] private float _pressDepth = 0.015f;
        [SerializeField] private float _pressDownTime = 0.08f;
        [SerializeField] private float _springTime = 0.12f;
        [Tooltip("Hand-to-cap distance that counts as hover (highlight).")]
        [SerializeField] private float _hoverDistance = 0.12f;
        [SerializeField] private float _debounce = 0.6f;

        [Header("Feedback")]
        [SerializeField] private string _clickKey = "ui_confirm";
        [SerializeField] private float _hapticAmplitude = 0.4f;
        [SerializeField] private float _hapticDuration = 0.1f;
        [SerializeField] private string _labelText = "PRESS TO BEGIN";
        [SerializeField] private TMP_FontAsset _font;

        private Transform _cap;
        private Material _capMat;
        private Vector3 _capHome;
        private Transform _label;
        private Transform _head;
        private ActionBasedController[] _controllers;
        private float _lastPress = -999f;
        private bool _animating;
        private float _glow;           // eased emissive 0..1
        private float _glowTarget;

        private static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly Color Accent = new Color(0.95f, 0.78f, 0.35f);
        private static readonly Color Housing = new Color(0.16f, 0.16f, 0.19f);

        private void Awake() { transform.localPosition = _localPosition; Build(); }

        private void Start()
        {
            _controllers = FindObjectsOfType<ActionBasedController>();
            var o = FindObjectOfType<XROrigin>();
            _head = (o != null && o.Camera != null) ? o.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
        }

        // --------------------------------------------------------------- build

        private void Build()
        {
            // Wide, dark housing.
            var housing = MakeCyl("Housing", new Vector3(0, 0f, 0), new Vector3(0.13f, 0.025f, 0.13f), Housing, false);
            // Raised accent press-cap (the moving part).
            var cap = MakeCyl("Cap", new Vector3(0, 0.032f, 0), new Vector3(0.10f, 0.012f, 0.10f), Accent, true);
            _cap = cap.transform;
            _capHome = _cap.localPosition;
            _capMat = cap.GetComponent<Renderer>().material; // instance
            _capMat.EnableKeyword("_EMISSION");
            _capMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // Direct-contact trigger volume at the cap (the hand enters this to press).
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.center = new Vector3(0, 0.04f, 0);
            sc.radius = 0.05f;

            BuildLabel();
            SetGlow(0f);
        }

        private GameObject MakeCyl(string name, Vector3 pos, Vector3 scale, Color color, bool emissive)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col); // visual only
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shader.Find("Standard"));
            m.SetColor(BaseColorID, color); m.color = color;
            if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor(EmissionID, Color.black); }
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private void BuildLabel()
        {
            var go = new GameObject("Label", typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420f, 90f);
            rt.localScale = Vector3.one * 0.0006f;
            go.transform.localPosition = new Vector3(0, 0.12f, 0);
            _label = go.transform;

            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(rt, false);
            var trt = t.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(420f, 90f);
            if (_font != null) t.font = _font;
            t.text = _labelText; t.fontSize = 54; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            var mat = t.fontMaterial;
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.02f, 0.03f, 1f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        }

        // ------------------------------------------------------------- runtime

        private void Update()
        {
            // Hover highlight from the nearest hand (not a press).
            float dMin = float.MaxValue;
            if (_controllers != null)
                foreach (var c in _controllers)
                    if (c != null) dMin = Mathf.Min(dMin, Vector3.Distance(c.transform.position, _cap.position));
            if (!_animating) _glowTarget = dMin <= _hoverDistance ? 0.5f : 0f;

            _glow = Mathf.Lerp(_glow, _glowTarget, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            SetGlow(_glow);
        }

        private void LateUpdate()
        {
            if (_label == null || _head == null) return;
            Vector3 dir = _label.position - _head.position; dir.y = 0f;   // readable billboard
            if (dir.sqrMagnitude > 1e-5f) _label.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsHand(other)) return;
            if (Time.time - _lastPress < _debounce) return;
            _lastPress = Time.time;
            StartCoroutine(PressRoutine());
        }

        private bool IsHand(Collider other)
        {
            // The hand's collision volume lives under the XR controller.
            return other.GetComponentInParent<ActionBasedController>() != null
                || other.GetComponentInParent<VRHandPoser>() != null;
        }

        private IEnumerator PressRoutine()
        {
            _animating = true;
            _glowTarget = 1f;

            // Multimodal feedback at the moment of contact.
            if (!string.IsNullOrEmpty(_clickKey) && Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.PlayUI(_clickKey, 1f);
            if (_controllers != null)
                foreach (var c in _controllers)
                    if (c != null) c.SendHapticImpulse(_hapticAmplitude, _hapticDuration);

            // Fire the progression event (Splash -> Atrium).
            EventBus.Publish(new ExperienceStartedEvent());

            // Piston down.
            yield return Move(_capHome, _capHome + Vector3.down * _pressDepth, _pressDownTime);
            // Spring back.
            yield return Move(_capHome + Vector3.down * _pressDepth, _capHome, _springTime);

            // Ease the press-glow back to idle over ~0.3 s.
            _animating = false;
            _glowTarget = 0f;
        }

        private IEnumerator Move(Vector3 from, Vector3 to, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                _cap.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            _cap.localPosition = to;
        }

        private void SetGlow(float k)
        {
            if (_capMat != null) _capMat.SetColor(EmissionID, Accent * Mathf.Lerp(0.05f, 1.4f, Mathf.Clamp01(k)));
        }
    }
}
