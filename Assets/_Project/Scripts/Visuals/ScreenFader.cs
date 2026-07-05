// -----------------------------------------------------------------------------
//  ScreenFader.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A VR-safe fade-to-black used between rooms. In VR you must NOT fade with a
//  full-screen post-process or a UI overlay on a world canvas — both cause
//  judder and "swimming" because they don't track head rotation perfectly.
//  The robust, widely-used technique is a small quad parented to the HMD camera,
//  rendered on top of everything via a constant-colour unlit shader with
//  per-instance alpha. Because it is rigidly attached to the eye, it stays
//  locked to the view no matter how the head moves, so the fade feels solid.
//
//  We drive alpha through a MaterialPropertyBlock (no material instancing, no
//  GC) and expose FadeOut/FadeIn coroutines that the SceneController awaits.
// -----------------------------------------------------------------------------

using System.Collections;
using UnityEngine;

namespace Decrypted.Visuals
{
    [DisallowMultipleComponent]
    public class ScreenFader : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The HMD camera. The fade quad is parented to this at runtime.")]
        [SerializeField] private Camera _hmdCamera;

        [Tooltip("Unlit, ZTest Always, transparent material used for the fade quad. " +
                 "If left empty one is generated from a built-in unlit shader at runtime.")]
        [SerializeField] private Material _fadeMaterial;

        [Header("Quad")]
        [Tooltip("Distance in front of the eye to place the quad (metres). Must sit " +
                 "inside the near plane region but in front of the near clip.")]
        [SerializeField] private float _quadDistance = 0.20f;

        [Tooltip("Half-extent of the quad. Generously oversized so it covers the FOV " +
                 "with margin even at the very wide Quest lenses.")]
        [SerializeField] private float _quadSize = 0.5f;

        [Header("Behaviour")]
        [Tooltip("Colour to fade to (black for the museum; could be white for a flash).")]
        [SerializeField] private Color _fadeColor = Color.black;

        [Tooltip("Start the experience fully black and fade in on first activation.")]
        [SerializeField] private bool _startOpaque = true;

        private static readonly int ColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIDLegacy = Shader.PropertyToID("_Color");

        private Transform _quad;
        private Renderer _quadRenderer;
        private MaterialPropertyBlock _mpb;
        private float _alpha;

        // ------------------------------------------------------------------ init

        private void Awake()
        {
            if (_hmdCamera == null) _hmdCamera = Camera.main;
            BuildQuad();
            _alpha = _startOpaque ? 1f : 0f;
            ApplyAlpha(_alpha);
        }

        private void BuildQuad()
        {
            // A simple two-triangle quad facing the camera. Built in code so the
            // project has no dependency on an imported mesh for something this small.
            var go = new GameObject("~ScreenFadeQuad");
            go.transform.SetParent(_hmdCamera != null ? _hmdCamera.transform : transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, _quadDistance);
            go.transform.localRotation = Quaternion.identity;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateQuadMesh(_quadSize);

            _quadRenderer = go.AddComponent<MeshRenderer>();
            _quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _quadRenderer.receiveShadows = false;
            _quadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _quadRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _quadRenderer.allowOcclusionWhenDynamic = false;
            _quadRenderer.sharedMaterial = _fadeMaterial != null ? _fadeMaterial : CreateRuntimeMaterial();

            _quad = go.transform;
            _mpb = new MaterialPropertyBlock();
        }

        private static Mesh CreateQuadMesh(float half)
        {
            var mesh = new Mesh { name = "ScreenFadeQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-half, -half, 0f),
                new Vector3( half, -half, 0f),
                new Vector3(-half,  half, 0f),
                new Vector3( half,  half, 0f),
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateRuntimeMaterial()
        {
            // Prefer the URP unlit shader; fall back to the built-in unlit colour.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = "ScreenFade (runtime)" };

            // Transparent, always-on-top, no depth write.
            mat.SetFloat("_Surface", 1f);          // URP: transparent
            mat.SetFloat("_ZWrite", 0f);
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return mat;
        }

        // --------------------------------------------------------------- public

        /// <summary>Current fade level: 0 = clear, 1 = fully opaque.</summary>
        public float Alpha => _alpha;

        public bool IsOpaque => _alpha >= 0.999f;

        /// <summary>Fade to opaque over <paramref name="seconds"/>.</summary>
        public IEnumerator FadeOut(float seconds) => FadeTo(1f, seconds);

        /// <summary>Fade to clear over <paramref name="seconds"/>.</summary>
        public IEnumerator FadeIn(float seconds) => FadeTo(0f, seconds);

        /// <summary>Snap instantly to a given alpha (used at boot).</summary>
        public void SetInstant(float alpha)
        {
            _alpha = Mathf.Clamp01(alpha);
            ApplyAlpha(_alpha);
        }

        // -------------------------------------------------------------- internal

        private IEnumerator FadeTo(float target, float seconds)
        {
            if (_quadRenderer == null) yield break;
            _quadRenderer.enabled = true;

            float start = _alpha;
            if (seconds <= 0f)
            {
                ApplyAlpha(target);
                _alpha = target;
            }
            else
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime / seconds; // unscaled: independent of Time.timeScale
                    _alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                    ApplyAlpha(_alpha);
                    yield return null;
                }
                _alpha = target;
                ApplyAlpha(_alpha);
            }

            // When fully clear we can disable the renderer to save a transparent draw.
            if (_alpha <= 0.001f) _quadRenderer.enabled = false;
        }

        private void ApplyAlpha(float a)
        {
            if (_quadRenderer == null) return;
            var c = _fadeColor; c.a = a;
            _quadRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorID, c);
            _mpb.SetColor(ColorIDLegacy, c); // covers both URP and built-in unlit
            _quadRenderer.SetPropertyBlock(_mpb);
            if (a > 0.001f) _quadRenderer.enabled = true;
        }
    }
}
