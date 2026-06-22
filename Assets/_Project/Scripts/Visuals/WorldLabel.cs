// -----------------------------------------------------------------------------
//  WorldLabel.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A floating, billboarding name+description label that hovers above an object.
//  This is the Unity translation of the requested troika-three-text labels:
//  TextMeshPro renders SDF, resolution-independent text that stays sharp at
//  headset resolution, which canvas-texture text cannot match.
//
//  Rules implemented exactly as specified:
//    * Two lines: a larger title (~0.04 world units) and an optional smaller
//      description (~0.028). Both center-aligned, clean sans-serif, white with a
//      thin dark outline so they read against any background.
//    * Floats 0.18-0.25 m above the object's topmost bounding point, always
//      upright in world space (never flat on a surface).
//    * Billboards every frame to face the player's current head position.
//    * Fades with distance: full opacity only between 0.8 and 2.5 m; smoothly
//      lerps to zero beyond 2.5 m and below 0.8 m (so it never fills the view
//      while grabbing). Linear interpolation, no hard cut.
//
//  Purely additive: attach it to an object, set the text, done. It never touches
//  gameplay, hands, time-scaling, or the panel system.
// -----------------------------------------------------------------------------

using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Decrypted.Visuals
{
    [DisallowMultipleComponent]
    public class WorldLabel : MonoBehaviour
    {
        [Header("Text (plain, short, no all-caps, no em dashes)")]
        [SerializeField] private string _title = "";
        [TextArea] [SerializeField] private string _description = "";
        [SerializeField] private TMP_FontAsset _font;     // clean sans-serif (Inter); null = TMP default

        [Header("Placement")]
        [Tooltip("Metres above the object's topmost point (0.18-0.25).")]
        [SerializeField] private float _heightAbove = 0.20f;

        [Header("Distance fade from head (metres)")]
        [SerializeField] private float _nearZero = 0.4f;  // closer than this -> hidden
        [SerializeField] private float _nearFull = 0.8f;  // full opacity from here
        [SerializeField] private float _farFull  = 2.5f;  // full opacity up to here
        [SerializeField] private float _farZero  = 3.5f;  // farther than this -> hidden

        [Header("Style")]
        [SerializeField] private float _titleSize = 40f;   // *0.001 = ~0.04 m
        [SerializeField] private float _descSize  = 28f;   // *0.001 = ~0.028 m
        [SerializeField] private float _outlineWidth = 0.2f;

        private Transform _head;
        private Transform _ui;
        private CanvasGroup _group;
        private Vector3 _anchorWorld;

        private Transform Head
        {
            get
            {
                if (_head == null)
                {
                    var o = FindObjectOfType<XROrigin>();
                    _head = (o != null && o.Camera != null) ? o.Camera.transform
                          : (Camera.main != null ? Camera.main.transform : null);
                }
                return _head;
            }
        }

        /// <summary>Set the label content before Start (used when attaching in code).</summary>
        public void Configure(string title, string description, float heightAbove, TMP_FontAsset font = null)
        {
            _title = title;
            _description = description;
            _heightAbove = heightAbove;
            if (font != null) _font = font;
        }

        private void Start()
        {
            _anchorWorld = ComputeTopAnchor();
            Build();
        }

        // Topmost point of all of this object's renderers, then lifted upright.
        private Vector3 ComputeTopAnchor()
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in rends)
            {
                // Runs before the label UI is built, so only the object's own
                // renderers exist here.
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            Vector3 top = has ? new Vector3(b.center.x, b.max.y, b.center.z) : transform.position;
            return top + Vector3.up * _heightAbove;
        }

        private void Build()
        {
            // Free-standing world-space canvas (0.001 scale => 1 unit == 1 mm),
            // so font sizes map directly to metres. Not parented to the object, so
            // a spinning/scaled exhibit never distorts the text.
            var go = new GameObject("WorldLabelUI", typeof(Canvas));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 320f);
            rt.localScale = Vector3.one * 0.001f;
            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _ui = go.transform;
            _ui.position = _anchorWorld;

            bool hasDesc = !string.IsNullOrEmpty(_description);
            MakeText("Title", rt, new Vector2(0, hasDesc ? 70f : 0f), new Vector2(680, 160), _title, _titleSize);
            if (hasDesc)
                MakeText("Desc", rt, new Vector2(0, -70f), new Vector2(680, 150), _description, _descSize);
        }

        private void MakeText(string name, Transform parent, Vector2 anchoredPos, Vector2 size, string text, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = true;
            t.raycastTarget = false;

            // Thin dark outline on a per-label material instance (so it never
            // pollutes the shared font material used elsewhere).
            var mat = t.fontMaterial;
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.02f, 0.03f, 1f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, _outlineWidth);
        }

        private void LateUpdate()
        {
            if (_ui == null) return;
            var head = Head;
            if (head == null) return;

            // Billboard: canvas front (+Z) points at the head, kept upright.
            Vector3 dir = head.position - _ui.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-5f)
                _ui.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            _group.alpha = OpacityForDistance(Vector3.Distance(head.position, _ui.position));
        }

        // Full opacity in [nearFull, farFull]; linear ramps out to nearZero / farZero.
        private float OpacityForDistance(float d)
        {
            if (d <= _nearZero || d >= _farZero) return 0f;
            if (d < _nearFull) return Mathf.InverseLerp(_nearZero, _nearFull, d);
            if (d > _farFull)  return 1f - Mathf.InverseLerp(_farFull, _farZero, d);
            return 1f;
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui.gameObject);
        }
    }
}
