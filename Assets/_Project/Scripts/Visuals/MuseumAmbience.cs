// -----------------------------------------------------------------------------
//  MuseumAmbience.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Two tiny, self-contained "make it feel alive" components used by the museum
//  dressing pass. Both are deliberately trivial and allocation-free at runtime so
//  they cost almost nothing on the Quest budget:
//
//    • MuseumSpin       — slowly rotates featured artifacts on their turntables.
//    • MuseumGlowPulse  — gently breathes a renderer's emission (display lights,
//                          status lamps, kiosk screens) via a MaterialPropertyBlock
//                          so no material is instanced and no GI rebuild occurs.
//
//  They are placed and configured from the editor builders (MuseumBuilder /
//  MuseumProps) and instanced into the scene, so both classes are in active use.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace Decrypted.Visuals
{
    /// <summary>Slowly rotates the object — used for turntable pedestals.</summary>
    [DisallowMultipleComponent]
    public class MuseumSpin : MonoBehaviour
    {
        [Tooltip("Degrees per second around Axis.")]
        public float DegreesPerSecond = 10f;
        [Tooltip("Local rotation axis.")]
        public Vector3 Axis = Vector3.up;

        private void Update()
        {
            transform.Rotate(Axis, DegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>Breathes the emission of one or more renderers between two scales.</summary>
    [DisallowMultipleComponent]
    public class MuseumGlowPulse : MonoBehaviour
    {
        [Tooltip("Renderers whose emission is pulsed. If empty, uses this object's renderer.")]
        public Renderer[] Targets;
        [Tooltip("Cycles per second.")]
        public float Speed = 0.4f;
        [Range(0f, 1f)] public float Min = 0.65f;
        [Range(0f, 4f)] public float Max = 1.25f;
        [Tooltip("Random phase so many lamps don't pulse in lockstep.")]
        public float Phase = 0f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private Color[] _base;

        private void Awake()
        {
            if (Targets == null || Targets.Length == 0)
            {
                var r = GetComponent<Renderer>();
                Targets = r != null ? new[] { r } : new Renderer[0];
            }
            _mpb = new MaterialPropertyBlock();
            _base = new Color[Targets.Length];
            for (int i = 0; i < Targets.Length; i++)
            {
                var m = Targets[i] != null ? Targets[i].sharedMaterial : null;
                _base[i] = (m != null && m.HasProperty(EmissionId)) ? m.GetColor(EmissionId) : Color.white;
            }
            if (Phase == 0f) Phase = Random.value * 6.283f;
        }

        private void Update()
        {
            float t = (Mathf.Sin(Time.time * Speed * 6.283f + Phase) * 0.5f) + 0.5f; // 0..1
            float k = Mathf.Lerp(Min, Max, t);
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i] == null) continue;
                Targets[i].GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, _base[i] * k);
                Targets[i].SetPropertyBlock(_mpb);
            }
        }
    }
}
