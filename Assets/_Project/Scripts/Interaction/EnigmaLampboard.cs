// -----------------------------------------------------------------------------
//  EnigmaLampboard.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 2)
//
//  The 26-lamp output board. When the keyboard encodes a letter, the matching
//  lamp glows — exactly like the real machine, where the operator read the
//  ciphertext off the lamps. Each lamp is an emissive renderer driven by a
//  MaterialPropertyBlock (no material instances), pulsed up then eased back down,
//  with a soft "lamp on" sound localised at the bulb.
// -----------------------------------------------------------------------------

using System.Collections;
using Decrypted.Managers;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class EnigmaLampboard : MonoBehaviour
    {
        [Header("Lamps (A..Z, 26 renderers)")]
        [SerializeField] private Renderer[] _lamps = new Renderer[26];

        [Header("Look")]
        [SerializeField] private Color _lampColor = new Color(1f, 0.86f, 0.45f, 1f);
        [Tooltip("Peak emissive multiplier.")]
        [SerializeField] private float _peak = 2.0f;
        [Tooltip("Seconds to ramp on.")]
        [SerializeField] private float _onSeconds = 0.05f;
        [Tooltip("Seconds the lamp holds before fading.")]
        [SerializeField] private float _holdSeconds = 0.25f;
        [Tooltip("Seconds to fade off.")]
        [SerializeField] private float _offSeconds = 0.35f;

        [Header("Audio")]
        [SerializeField] private string _lampSfxKey = "sfx_lamp_on";

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private Coroutine[] _routines;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _routines = new Coroutine[26];
            AllOff();
        }

        /// <summary>Glow the lamp for a letter A-Z.</summary>
        public void Light(char c)
        {
            c = char.ToUpperInvariant(c);
            if (c < 'A' || c > 'Z') return;
            int idx = c - 'A';
            if (idx >= _lamps.Length || _lamps[idx] == null) return;

            if (_routines[idx] != null) StopCoroutine(_routines[idx]);
            _routines[idx] = StartCoroutine(Flash(idx));

            if (!string.IsNullOrEmpty(_lampSfxKey) && AudioManager.Instance != null)
                AudioManager.Instance.Play(_lampSfxKey, _lamps[idx].transform.position, true, 0.8f);
        }

        public void AllOff()
        {
            // Stop any in-flight flash coroutines first, otherwise a running Flash would
            // keep re-lighting the lamp after we zero it (the "lamp still glows" bug).
            if (_routines != null)
                for (int i = 0; i < _routines.Length; i++)
                    if (_routines[i] != null) { StopCoroutine(_routines[i]); _routines[i] = null; }
            for (int i = 0; i < _lamps.Length; i++) SetLamp(i, 0f);
        }

        private IEnumerator Flash(int idx)
        {
            float t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _onSeconds); SetLamp(idx, _peak * t); yield return null; }
            SetLamp(idx, _peak);
            yield return new WaitForSecondsRealtime(_holdSeconds);
            t = 0f;
            while (t < 1f) { t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _offSeconds); SetLamp(idx, _peak * (1f - t)); yield return null; }
            SetLamp(idx, 0f);
            _routines[idx] = null;
        }

        private void SetLamp(int idx, float intensity)
        {
            if (idx < 0 || idx >= _lamps.Length || _lamps[idx] == null) return;
            _lamps[idx].GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorID, _lampColor * Mathf.Max(0f, intensity));
            _lamps[idx].SetPropertyBlock(_mpb);
        }
    }
}
