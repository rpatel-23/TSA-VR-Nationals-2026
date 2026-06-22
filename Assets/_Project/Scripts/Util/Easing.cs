// -----------------------------------------------------------------------------
//  Easing.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A small, dependency-free easing library so every exhibit animation can pick a
//  curve that gives its motion CHARACTER instead of the one bland smoothstep the
//  whole project used to share. Curves follow the standard definitions used by
//  https://easings.net so the names match what artists already know.
//
//  Usage (drop-in replacement for `Mathf.SmoothStep(0f, 1f, t)`):
//      float k = Easing.Evaluate(_myEaseField, t);   // t in [0,1]
//      value = Mathf.Lerp(a, b, k);
//
//  Expose an `Ease` field with [SerializeField] and you can swap the feel of a
//  motion live in the Inspector — no recompile — which is the fast way to try
//  several versions of an animation and keep the one that looks best in headset.
//
//  Note: BackOut / ElasticOut / BounceOut intentionally return values that briefly
//  exceed 1 (overshoot) before settling to exactly 1 at t=1. That overshoot is the
//  point — it's what makes a dial "click" or a door "thunk" into place. Drive
//  positions/rotations with them, not things that must stay clamped (e.g. alpha).
// -----------------------------------------------------------------------------

using UnityEngine;

namespace Decrypted.Util
{
    /// <summary>Selectable easing curve. Pick in the Inspector to retune a motion's feel.</summary>
    public enum Ease
    {
        Linear,        // constant speed (robotic — usually avoid)
        Smooth,        // smoothstep: the project's old default ease-in-out
        Smoother,      // smootherstep (Perlin): softer ends than Smooth
        SineInOut,     // gentle, organic ease-in-out
        QuadOut,       // quick start, soft landing
        CubicIn,       // slow start that builds — a deliberate, weighted pull
        CubicInOut,    // weighty ease-in-out
        QuintInOut,    // very heavy ease-in-out (a 2-ton door)
        ExpoOut,       // snaps fast then glides home — mechanical
        BackOut,       // overshoots then settles — a tactile "click into place"
        ElasticOut,    // springs past and oscillates to rest
        BounceOut      // bounces to rest like dropped metal
    }

    public static class Easing
    {
        // Overshoot constant shared by the Back curves (standard easings.net value).
        private const float C1 = 1.70158f;
        private const float C3 = C1 + 1f;

        /// <summary>Evaluate the given curve at t (clamped to [0,1]).</summary>
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Linear:     return t;
                case Ease.Smooth:     return t * t * (3f - 2f * t);
                case Ease.Smoother:   return t * t * t * (t * (t * 6f - 15f) + 10f);
                case Ease.SineInOut:  return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case Ease.QuadOut:    return 1f - (1f - t) * (1f - t);
                case Ease.CubicIn:    return t * t * t;
                case Ease.CubicInOut: return t < 0.5f
                                          ? 4f * t * t * t
                                          : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.QuintInOut: return t < 0.5f
                                          ? 16f * t * t * t * t * t
                                          : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f;
                case Ease.ExpoOut:    return Mathf.Approximately(t, 1f)
                                          ? 1f
                                          : 1f - Mathf.Pow(2f, -10f * t);
                case Ease.BackOut:    return 1f + C3 * Mathf.Pow(t - 1f, 3f) + C1 * Mathf.Pow(t - 1f, 2f);
                case Ease.ElasticOut: return ElasticOut(t);
                case Ease.BounceOut:  return BounceOut(t);
                default:              return t;
            }
        }

        private static float ElasticOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        private static float BounceOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1)       return n1 * t * t;
            if (t < 2f / d1)     { t -= 1.5f / d1;  return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1)   { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;      return n1 * t * t + 0.984375f;
        }
    }
}
