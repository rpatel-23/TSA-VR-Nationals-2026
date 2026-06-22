// -----------------------------------------------------------------------------
//  RoomDoor.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  A COSMETIC exit door. It does not gate progression in any way - there is no
//  handle, no grab, no grip, no button. Progression is fully automatic (see
//  AutoProgressionController + the countdown popup). When a room's advance begins,
//  the controller calls PlayExitFlourish() and the door simply lights up (emissive
//  pulse, no realtime lights) and swings open as a visual flourish synced to the
//  countdown. The door never calls Advance and never blocks the player.
//
//  Built from simple low-poly white geometry (frame + slab + a decorative handle)
//  in the SUPERHOT style. Geometry can be previewed in edit mode via the context
//  menu and is rebuilt fresh at runtime. Placement is the ExitDoor transform
//  itself (per-room, editable in the Inspector / Scene view).
// -----------------------------------------------------------------------------

using System.Collections;
using Decrypted.Core;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class RoomDoor : MonoBehaviour
    {
        [Header("Which room this door belongs to")]
        [SerializeField] private MuseumState _room;
        public MuseumState Room => _room;

        [Header("Dimensions (metres)")]
        [SerializeField] private float _openingWidth = 0.95f;
        [SerializeField] private float _openingHeight = 2.05f;
        [SerializeField] private float _handleHeight = 0.95f;

        [Header("Auto-open flourish (cosmetic, never gates)")]
        [SerializeField] private float _swingAngle = 90f;
        [SerializeField] private float _swingSeconds = 1.2f;

        [Header("Activation glow (emissive only - no realtime lights)")]
        [SerializeField] private float _pulseCycle = 1.5f;
        [SerializeField] private float _pulseMinEmissive = 0.12f;
        [SerializeField] private float _pulseMaxEmissive = 1.4f;

        private Transform _slabPivot;
        private Material _frameMat;
        private bool _glowing;

        private static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly Color OffWhite   = new Color(0.86f, 0.85f, 0.82f);
        private static readonly Color FrameInert = new Color(0.30f, 0.30f, 0.33f);
        private static readonly Color GlowWhite  = Color.white;

        private void Awake() => Build();

        /// <summary>Cosmetic only: light the frame and swing the slab open. Never advances.</summary>
        public void PlayExitFlourish()
        {
            _glowing = true;
            StartCoroutine(SwingOpen());
        }

        private IEnumerator SwingOpen()
        {
            if (_slabPivot == null) yield break;
            Quaternion from = _slabPivot.localRotation;
            Quaternion to = Quaternion.Euler(0f, _swingAngle, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _swingSeconds);
                _slabPivot.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            _slabPivot.localRotation = to;
        }

        private void Update()
        {
            if (!_glowing || _frameMat == null) return;
            float pulse = (Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / Mathf.Max(0.1f, _pulseCycle))) + 1f) * 0.5f;
            _frameMat.SetColor(EmissionID, GlowWhite * Mathf.Lerp(_pulseMinEmissive, _pulseMaxEmissive, pulse));
        }

        // ------------------------------------------------------------- geometry

        [ContextMenu("Rebuild Door (editor preview)")]
        private void RebuildInEditor() => Build();

        private void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }

            float w = _openingWidth, h = _openingHeight;
            float post = 0.09f, depth = 0.12f;

            _frameMat = MakeMat(FrameInert, true);
            var slabMat = MakeMat(OffWhite, false);
            var handleMat = MakeMat(OffWhite, false);

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
            // Decorative handle only - no collider, no interactable.
            MakeBox("Handle", slab.transform, new Vector3(w * 0.5f - 0.10f, _handleHeight, 0.05f), new Vector3(0.05f, 0.16f, 0.05f), handleMat);
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
