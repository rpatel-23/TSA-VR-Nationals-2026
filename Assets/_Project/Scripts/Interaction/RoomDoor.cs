// -----------------------------------------------------------------------------
//  RoomDoor.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A physical exit door. Replaces the old "Let's Go" notification panel as the
//  ONLY way the player advances between rooms. One door per room, built from
//  simple low-poly white geometry (frame + slab + handle) in the SUPERHOT style.
//
//  Lifecycle:
//    * Inert until the room's win condition is met: darker frame, no glow, the
//      handle cannot be grabbed, no NEXT label.
//    * On the win condition it ACTIVATES: the frame glow pulses on a ~1.5s cycle
//      (emissive + point light), a short spatial tone plays from the door, and a
//      billboarding "NEXT" label appears above the frame.
//    * The handle is the grab point and uses the existing collision/grip hand
//      system (an XR interactable). Within ~0.35 m it highlights warm white; grip
//      it and the slab swings open 90 degrees (ease in out, 0.6s), then the
//      experience advances (the SceneController fades to black, loads the next
//      room, and fades back in). The last room advances to the completion state.
//
//  Purely a progression-trigger replacement: it does not touch hands, the time
//  scaling system, narrator audio, the WorldLabel system, or room content.
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
        [Tooltip("Rooms with no puzzle (Splash, Atrium, Reveal) activate after a " +
                 "short dwell on entry; puzzle rooms activate when solved.")]
        [SerializeField] private bool _activateOnDwell = false;
        [SerializeField] private float _dwellSeconds = 4f;

        [Header("Dimensions (metres)")]
        [SerializeField] private float _openingWidth = 0.95f;
        [SerializeField] private float _openingHeight = 2.05f;
        [Tooltip("Handle height from the local floor (0.9-1.0).")]
        [SerializeField] private float _handleHeight = 0.95f;

        [Header("Open animation")]
        [SerializeField] private float _swingAngle = 90f;
        [SerializeField] private float _swingSeconds = 0.6f;

        [Header("Audio (short clean tone on activation)")]
        [SerializeField] private string _activateSfxKey = "sfx_lamp_on";
        [SerializeField] private float _activateVolume = 0.6f;

        [Header("Label font (clean sans-serif, e.g. Inter)")]
        [SerializeField] private TMP_FontAsset _font;

        // ---- built references ----
        private Transform _slabPivot;
        private Light _frameLight;
        private XRSimpleInteractable _handle;
        private CanvasGroup _labelGroup;
        private Transform _label;
        private Material _frameMat;
        private Material _handleMat;

        private bool _built, _active, _opening, _hovering;
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

        private void Awake() => Build();

        private void OnEnable()
        {
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Subscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Unsubscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private void Start()
        {
            // Splash is set instantly at boot (no RoomEnteredEvent), so begin here
            // if we are already in this door's room.
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
            // puzzle rooms simply wait for OnExhibitSolved.
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

            if (_frameLight != null) _frameLight.enabled = true;
            if (_handle != null) _handle.enabled = true;
            if (_labelGroup != null) _labelGroup.alpha = 1f;

            if (!string.IsNullOrEmpty(_activateSfxKey) && Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.Play(_activateSfxKey, transform.position, true, _activateVolume);
        }

        // --------------------------------------------------------------- update

        private void Update()
        {
            if (!_active) return;

            // Slow ~1.5s glow pulse on the frame (unscaled so time-scaling never
            // freezes it). Suppressed while the door is mid-swing.
            float pulse = _opening ? 0f : (Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / 1.5f)) + 1f) * 0.5f;
            if (_frameMat != null)
                _frameMat.SetColor(EmissionID, GlowWhite * Mathf.Lerp(0.12f, 1.4f, pulse));
            if (_frameLight != null)
                _frameLight.intensity = Mathf.Lerp(0.3f, 2.2f, pulse);

            // Handle highlight when the hand is in proximity (hover).
            if (_handleMat != null)
            {
                float warm = _hovering ? 1.3f : 0.15f;
                _handleMat.SetColor(EmissionID, HandleWarm * warm);
            }
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
            // Swing the slab on its hinge with ease-in-out.
            Quaternion from = Quaternion.identity;
            Quaternion to = Quaternion.Euler(0f, _swingAngle, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _swingSeconds);
                _slabPivot.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            _slabPivot.localRotation = to;
            if (_labelGroup != null) _labelGroup.alpha = 0f;

            // Hand off to the existing transition: fade to black, load the next
            // room, fade back in. From the last room this advances to Complete.
            GameManager.Instance?.Advance();
        }

        // ------------------------------------------------------------- geometry

        private void Build()
        {
            if (_built) return;
            _built = true;

            float w = _openingWidth, h = _openingHeight;
            float post = 0.09f, depth = 0.12f;

            _frameMat  = MakeMat(FrameInert, true);
            var slabMat = MakeMat(OffWhite, false);
            _handleMat = MakeMat(OffWhite, true);

            // Frame: two posts + a lintel (all share the glowing frame material).
            MakeBox("Post_L", transform, new Vector3(-(w * 0.5f + post * 0.5f), h * 0.5f, 0f), new Vector3(post, h, depth), _frameMat);
            MakeBox("Post_R", transform, new Vector3(+(w * 0.5f + post * 0.5f), h * 0.5f, 0f), new Vector3(post, h, depth), _frameMat);
            MakeBox("Lintel", transform, new Vector3(0f, h + post * 0.5f, 0f), new Vector3(w + post * 2f, post, depth), _frameMat);

            // Hinge pivot on the left edge; unscaled parents so meshes never distort.
            var pivot = new GameObject("SlabPivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = new Vector3(-w * 0.5f, 0f, 0f);
            _slabPivot = pivot.transform;

            var slab = new GameObject("Slab");
            slab.transform.SetParent(_slabPivot, false);
            slab.transform.localPosition = new Vector3(w * 0.5f, 0f, 0f);   // recentre in opening
            MakeBox("SlabMesh", slab.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3(w * 0.94f, h * 0.95f, 0.05f), slabMat);

            // Handle near the latch edge, at hand height, nudged toward the player.
            var handle = new GameObject("Handle");
            handle.transform.SetParent(slab.transform, false);
            handle.transform.localPosition = new Vector3(w * 0.5f - 0.10f, _handleHeight, 0.05f);
            MakeBox("HandleMesh", handle.transform, new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.16f, 0.05f), _handleMat);
            var sc = handle.AddComponent<SphereCollider>();
            sc.isTrigger = true;          // detected by the hand's direct interactor; never blocks movement
            sc.radius = 0.25f;            // ~0.35 m effective grab with the hand sphere
            _handle = handle.AddComponent<XRSimpleInteractable>();
            _handle.enabled = false;      // inert until the door activates
            _handle.hoverEntered.AddListener(_ => _hovering = true);
            _handle.hoverExited.AddListener(_ => _hovering = false);
            _handle.selectEntered.AddListener(_ => Open());

            // Frame glow light.
            var lightGO = new GameObject("FrameLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = new Vector3(0f, h * 0.6f, 0.15f);
            _frameLight = lightGO.AddComponent<Light>();
            _frameLight.type = LightType.Point;
            _frameLight.color = GlowWhite;
            _frameLight.range = 2.5f;
            _frameLight.intensity = 0f;
            _frameLight.enabled = false;

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
            t.fontSize = 60f;                 // *0.001 = ~0.06 m
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
            if (col != null) Destroy(col);          // visual only; never blocks the player
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
