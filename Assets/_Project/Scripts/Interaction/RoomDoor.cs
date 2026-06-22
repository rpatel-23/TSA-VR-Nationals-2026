// -----------------------------------------------------------------------------
//  RoomDoor.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A physical exit door. The ONLY way the player advances between rooms. One door
//  per room, built from simple low-poly white geometry (frame + slab + handle) in
//  the SUPERHOT style.
//
//  Lifecycle:
//    * Inert until the room's win condition is met: darker frame, no glow, the
//      handle cannot be grabbed, no NEXT label.
//    * On the win condition it ACTIVATES: the frame EMISSIVE pulses on a tunable
//      cycle (NO realtime lights - this project is baked-lighting / 72 FPS), a
//      short spatial tone plays from the door, and a billboarding "NEXT" label
//      appears above the frame.
//    * The handle is the grab point and uses the existing collision/grip hand
//      system (an XR interactable + trigger sphere of _handleReach radius). Within
//      reach it highlights warm white; grip it and the slab swings open (ease in
//      out), then the experience advances via the existing ScreenFader transition
//      (no second fade system). The last room advances to the Complete state.
//
//  Placement is the ExitDoor Transform itself (per-room, editable in the Inspector
//  / Scene view). Geometry can be previewed/tuned in edit mode via the context
//  menu and is rebuilt fresh at runtime. Purely a progression-trigger mechanism:
//  it does not touch hands, time-scaling, narrator audio, the WorldLabel system,
//  or room content.
// -----------------------------------------------------------------------------

using System.Collections;
using Decrypted.Core;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class RoomDoor : MonoBehaviour
    {
        [Header("Which room this door exits")]
        [SerializeField] private MuseumState _room;
        [Tooltip("Rooms with no puzzle (Splash, Atrium) activate after a short dwell " +
                 "on entry; puzzle/reveal rooms activate on their win event.")]
        [SerializeField] private bool _activateOnDwell = false;
        [SerializeField] private float _dwellSeconds = 4f;

        [Header("Dimensions (metres) - tunable per door")]
        [SerializeField] private float _openingWidth = 0.95f;
        [SerializeField] private float _openingHeight = 2.05f;
        [Tooltip("Handle height from the local floor (0.9-1.0).")]
        [SerializeField] private float _handleHeight = 0.95f;
        [Tooltip("Radius of the handle's grab trigger - the reach distance (m).")]
        [SerializeField] private float _handleReach = 0.35f;

        [Header("Open animation - tunable per door")]
        [SerializeField] private float _swingAngle = 90f;
        [SerializeField] private float _swingSeconds = 0.6f;

        [Header("Activation glow (emissive only - no realtime lights)")]
        [Tooltip("Full pulse cycle in seconds (~1.5).")]
        [SerializeField] private float _pulseCycle = 1.5f;
        [SerializeField] private float _pulseMinEmissive = 0.12f;
        [SerializeField] private float _pulseMaxEmissive = 1.4f;

        [Header("Audio (short clean tone on activation)")]
        [SerializeField] private string _activateSfxKey = "sfx_lamp_on";
        [SerializeField] private float _activateVolume = 0.6f;

        [Header("Label font (clean sans-serif, e.g. Inter)")]
        [SerializeField] private TMP_FontAsset _font;

        // ---- runtime references (re-resolved on each Build) ----
        private Transform _slabPivot;
        private XRSimpleInteractable _handle;
        private CanvasGroup _labelGroup;
        private Transform _label;
        private Material _frameMat;
        private Material _handleMat;

        private bool _active, _opening, _hovering;
        private Transform _head;

        private static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        private static readonly Color OffWhite   = new Color(0.86f, 0.85f, 0.82f);
        private static readonly Color FrameInert = new Color(0.30f, 0.30f, 0.33f);
        private static readonly Color GlowWhite  = Color.white;
        private static readonly Color HandleWarm = new Color(1.0f, 0.88f, 0.70f);

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

        // ----------------------------------------------------------- lifecycle

        private void Awake() => Build();   // runtime: rebuild fresh (clears any editor preview)

        private void OnEnable()
        {
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Subscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Unsubscribe<ExhibitSolvedEvent>(OnExhibitSolved);
            // Defensive teardown: stop any in-flight swing and drop references so a
            // mid-transition deactivation can never dereference a destroyed object.
            StopAllCoroutines();
            _opening = false;
        }

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == _room)
                BeginRoom();
        }

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            if (e.Room == _room) BeginRoom();
        }

        private void OnExhibitSolved(ExhibitSolvedEvent e)
        {
            if (e.Room == _room && !_activateOnDwell) Activate();
        }

        private void BeginRoom()
        {
            if (_active) return;
            if (_activateOnDwell) StartCoroutine(DwellThenActivate());
        }

        private IEnumerator DwellThenActivate()
        {
            float t = 0f;
            while (t < _dwellSeconds)
            {
                if (GameManager.Instance == null || GameManager.Instance.CurrentState != _room) yield break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Activate();
        }

        // ------------------------------------------------------------- activate

        private void Activate()
        {
            if (_active) return;
            _active = true;

            if (_handle != null) _handle.enabled = true;
            if (_labelGroup != null) _labelGroup.alpha = 1f;

            if (!string.IsNullOrEmpty(_activateSfxKey) && Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.Play(_activateSfxKey, transform.position, true, _activateVolume);
        }

        // --------------------------------------------------------------- update

        private void Update()
        {
            if (!_active) return;

            // Emissive-only pulse (unscaled so time-scaling can never freeze it).
            // No realtime lights are used anywhere on the door.
            float pulse = _opening ? 0f
                : (Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / Mathf.Max(0.1f, _pulseCycle))) + 1f) * 0.5f;
            if (_frameMat != null)
                _frameMat.SetColor(EmissionID, GlowWhite * Mathf.Lerp(_pulseMinEmissive, _pulseMaxEmissive, pulse));

            if (_handleMat != null)
                _handleMat.SetColor(EmissionID, HandleWarm * (_hovering ? 1.3f : 0.15f));
        }

        private void LateUpdate()
        {
            if (!_active || _label == null) return;
            var head = Head;
            if (head == null) return;
            Vector3 dir = head.position - _label.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-5f)
                _label.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // ----------------------------------------------------------- open / swing

        private void Open()
        {
            if (!_active || _opening) return;
            _opening = true;
            if (_handle != null) _handle.enabled = false;
            StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            Quaternion from = Quaternion.identity;
            Quaternion to = Quaternion.Euler(0f, _swingAngle, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _swingSeconds);
                if (_slabPivot != null)
                    _slabPivot.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            if (_slabPivot != null) _slabPivot.localRotation = to;
            if (_labelGroup != null) _labelGroup.alpha = 0f;

            // Existing ScreenFader transition: fade to black, load next room, fade in.
            // From the last room this advances to the Complete state.
            if (GameManager.Instance != null) GameManager.Instance.Advance();
        }

        // ------------------------------------------------------------- geometry

        [ContextMenu("Rebuild Door (editor preview)")]
        private void RebuildInEditor() => Build();

        private void Build()
        {
            // Clear any previously built geometry so this is idempotent (editor
            // preview rebuilds, runtime rebuilds over an editor preview). The
            // ExitDoor object holds nothing but door parts.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }

            float w = _openingWidth, h = _openingHeight;
            float post = 0.09f, depth = 0.12f;

            _frameMat  = MakeMat(FrameInert, true);
            var slabMat = MakeMat(OffWhite, false);
            _handleMat = MakeMat(OffWhite, true);

            MakeBox("Post_L", transform, new Vector3(-(w * 0.5f + post * 0.5f), h * 0.5f, 0f), new Vector3(post, h, depth), _frameMat);
            MakeBox("Post_R", transform, new Vector3(+(w * 0.5f + post * 0.5f), h * 0.5f, 0f), new Vector3(post, h, depth), _frameMat);
            MakeBox("Lintel", transform, new Vector3(0f, h + post * 0.5f, 0f), new Vector3(w + post * 2f, post, depth), _frameMat);

            var pivot = new GameObject("SlabPivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = new Vector3(-w * 0.5f, 0f, 0f);
            _slabPivot = pivot.transform;

            var slab = new GameObject("Slab");
            slab.transform.SetParent(_slabPivot, false);
            slab.transform.localPosition = new Vector3(w * 0.5f, 0f, 0f);
            MakeBox("SlabMesh", slab.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3(w * 0.94f, h * 0.95f, 0.05f), slabMat);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(slab.transform, false);
            handle.transform.localPosition = new Vector3(w * 0.5f - 0.10f, _handleHeight, 0.05f);
            MakeBox("HandleMesh", handle.transform, Vector3.zero, new Vector3(0.05f, 0.16f, 0.05f), _handleMat);
            var sc = handle.AddComponent<SphereCollider>();
            sc.isTrigger = true;                 // detected by the hand's direct interactor; never blocks movement
            sc.radius = Mathf.Max(0.05f, _handleReach);
            _handle = handle.AddComponent<XRSimpleInteractable>();
            _handle.enabled = false;             // inert until the door activates

            // Runtime-only wiring (lambdas do not serialize, so we add them whenever
            // Build runs at runtime; editor preview leaves the handle inert).
            if (Application.isPlaying)
            {
                _handle.hoverEntered.AddListener(_ => _hovering = true);
                _handle.hoverExited.AddListener(_ => _hovering = false);
                _handle.selectEntered.AddListener(_ => Open());
            }

            BuildLabel(h);
        }

        private void BuildLabel(float h)
        {
            var go = new GameObject("NextLabel", typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 160f);
            rt.localScale = Vector3.one * 0.001f;
            go.transform.localPosition = new Vector3(0f, h + 0.2f, 0f);
            _labelGroup = go.AddComponent<CanvasGroup>();
            _labelGroup.alpha = 0f;
            _label = go.transform;

            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(rt, false);
            var trt = t.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(400f, 160f);
            if (_font != null) t.font = _font;
            t.text = "NEXT";
            t.fontSize = 60f;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            var mat = t.fontMaterial;
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.02f, 0.03f, 1f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        }

        private Material MakeMat(Color baseColor, bool emissive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shader.Find("Standard"));
            m.SetColor(BaseColorID, baseColor);
            m.color = baseColor;
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor(EmissionID, Color.black);
            }
            return m;
        }

        private void MakeBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
