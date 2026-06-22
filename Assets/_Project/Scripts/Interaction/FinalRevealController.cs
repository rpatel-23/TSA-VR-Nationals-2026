// -----------------------------------------------------------------------------
//  FinalRevealController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Final chamber)
//
//  The closing beat. A central sculpture morphs through the three eras the museum
//  has just taught — a Roman inscription, then clockwork Enigma gears, then a
//  modern circuit lattice — while the conclusion plaque fades up and a final
//  chord resolves. When the morph completes the experience advances to Complete.
//
//  The morph is driven by a single 0..1 progress value, exposed two ways so the
//  artist can pick whichever asset pipeline they built:
//
//   * STAGE CROSS-FADE (default): three stacked stage renderers (Roman / Gears /
//     Circuit) are cross-dissolved across the progress range using a "_Dissolve"
//     float and an emissive bloom. Robust, needs no rig.
//   * BLENDSHAPE: if a SkinnedMeshRenderer with two blendshapes (toGears,
//     toCircuit) is assigned, its weights are driven by the same progress for a
//     single continuously-morphing mesh.
//
//  The conclusion text is fixed by the brief and reproduced verbatim.
// -----------------------------------------------------------------------------

using System.Collections;
using Decrypted.Core;
using Decrypted.Managers;
using Decrypted.Util;
using TMPro;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class FinalRevealController : MonoBehaviour
    {
        // Fixed by the design brief — do not paraphrase.
        private const string ConclusionText =
            "From Caesar's alphabet shifts to modern digital security, cryptography " +
            "protects information by transforming meaning into secrets only the " +
            "intended recipient can reveal.";

        [Header("Trigger")]
        [Tooltip("Seconds after entering the chamber before the reveal begins.")]
        [SerializeField] private float _autoStartDelay = 1.5f;
        [Tooltip("If false, call BeginReveal() yourself (e.g. from a plinth poke).")]
        [SerializeField] private bool _autoStartOnEnter = true;

        [Header("Morph — stage cross-fade")]
        [Tooltip("Exactly three stage renderers: [0]=Roman, [1]=Gears, [2]=Circuit.")]
        [SerializeField] private Renderer[] _stages = new Renderer[3];
        [SerializeField] private Color _stageEmissive = new Color(0.85f, 0.7f, 0.4f, 1f);
        [SerializeField] private float _stageEmissivePeak = 1.3f;

        [Header("Morph — optional blendshape mesh")]
        [SerializeField] private SkinnedMeshRenderer _morphMesh;
        [SerializeField] private int _toGearsBlendIndex = 0;
        [SerializeField] private int _toCircuitBlendIndex = 1;

        [Header("Motion")]
        [SerializeField] private float _morphSeconds = 7.0f;
        [Tooltip("Curve for the era morph. SineInOut eases the transformation in and " +
                 "out so it breathes instead of crossfading at a constant rate.")]
        [SerializeField] private Ease _morphEase = Ease.SineInOut;
        [Tooltip("Gentle rotation of the sculpture during the reveal (deg/sec).")]
        [SerializeField] private float _spinSpeed = 12f;
        [SerializeField] private Transform _spinRoot;

        [Header("Conclusion plaque")]
        [SerializeField] private TMP_Text _conclusionLabel;
        [SerializeField] private CanvasGroup _conclusionGroup;
        [SerializeField] private float _plaqueFadeSeconds = 2.5f;
        [Tooltip("Fraction of the morph (0..1) at which the plaque begins to fade in.")]
        [Range(0f, 1f)] [SerializeField] private float _plaqueFadeStart = 0.55f;

        [Header("Audio")]
        [SerializeField] private string _finalChordKey = "sfx_final_chord";

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int DissolveID = Shader.PropertyToID("_Dissolve");
        private MaterialPropertyBlock _mpb;
        private bool _started;
        private bool _chordPlayed;
        private Vector3[] _stageBaseScales;

        // ----------------------------------------------------------------- life

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            // Cache each stage's authored scale so the morph can grow/shrink them
            // smoothly instead of popping renderers on and off.
            if (_stages != null)
            {
                _stageBaseScales = new Vector3[_stages.Length];
                for (int i = 0; i < _stages.Length; i++)
                    if (_stages[i] != null) _stageBaseScales[i] = _stages[i].transform.localScale;
            }
            if (_conclusionLabel != null) _conclusionLabel.text = ConclusionText;
            if (_conclusionGroup != null) _conclusionGroup.alpha = 0f;
            if (_spinRoot == null) _spinRoot = transform;
            ApplyMorph(0f);     // start fully Roman
        }

        private void OnEnable() => EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        private void OnDisable() => EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            if (_autoStartOnEnter && e.Room == MuseumState.RevealChamber)
                BeginReveal();
        }

        // ------------------------------------------------------------- reveal

        /// <summary>Kick off the reveal (idempotent).</summary>
        public void BeginReveal()
        {
            if (_started) return;
            _started = true;
            StartCoroutine(RevealRoutine());
        }

        private IEnumerator RevealRoutine()
        {
            yield return new WaitForSeconds(_autoStartDelay);

            // Final chord swells right as the transformation begins.
            if (!_chordPlayed && !string.IsNullOrEmpty(_finalChordKey) && AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(_finalChordKey, _spinRoot.position, true, 1f);
                _chordPlayed = true;
            }

            float t = 0f;
            bool plaqueStarted = false;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _morphSeconds);
                float p = Mathf.Clamp01(t);
                ApplyMorph(Easing.Evaluate(_morphEase, p));

                if (_spinRoot != null)
                    _spinRoot.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);

                if (!plaqueStarted && p >= _plaqueFadeStart)
                {
                    plaqueStarted = true;
                    StartCoroutine(FadePlaqueIn());
                }
                yield return null;
            }
            ApplyMorph(1f);

            // Make sure the plaque is fully up even on very short morph times.
            if (!plaqueStarted) yield return FadePlaqueIn();

            // Hold the final tableau briefly, then signal the win condition. The
            // ProgressionGateController shows the "Let's Go" panel; the player
            // presses it to finish (RevealChamber -> Complete). No auto-advance.
            yield return new WaitForSeconds(2.0f);
            EventBus.Publish(new ExhibitSolvedEvent(MuseumState.RevealChamber));
        }

        /// <summary>Map progress 0..1 onto the three stages (and the blendshapes).
        /// 0.0–0.5 morphs Roman→Gears, 0.5–1.0 morphs Gears→Circuit.</summary>
        private void ApplyMorph(float p)
        {
            // Per-stage "presence" 0..1.
            float roman = 1f - Mathf.Clamp01(p / 0.5f);
            float gears = p < 0.5f ? Mathf.Clamp01(p / 0.5f)
                                   : 1f - Mathf.Clamp01((p - 0.5f) / 0.5f);
            float circuit = Mathf.Clamp01((p - 0.5f) / 0.5f);

            SetStage(0, roman);
            SetStage(1, gears);
            SetStage(2, circuit);

            if (_morphMesh != null)
            {
                // toGears ramps 0->100 over first half then holds; toCircuit over second half.
                float toGears = Mathf.Clamp01(p / 0.5f) * 100f;
                float toCircuit = Mathf.Clamp01((p - 0.5f) / 0.5f) * 100f;
                if (_toGearsBlendIndex >= 0) _morphMesh.SetBlendShapeWeight(_toGearsBlendIndex, toGears);
                if (_toCircuitBlendIndex >= 0) _morphMesh.SetBlendShapeWeight(_toCircuitBlendIndex, toCircuit);
            }
        }

        private void SetStage(int idx, float presence)
        {
            if (_stages == null || idx < 0 || idx >= _stages.Length || _stages[idx] == null) return;
            var r = _stages[idx];

            // Dissolve: 0 = solid, 1 = gone. Emissive blooms near the hand-off.
            float p = Mathf.Clamp01(presence);
            float dissolve = 1f - p;
            float bloom = Mathf.Sin(p * Mathf.PI) * _stageEmissivePeak;

            // Smoothly scale the stage in/out so the morph reads as a transformation
            // rather than a hard pop (the URP/Lit materials have no _Dissolve).
            if (_stageBaseScales != null && idx < _stageBaseScales.Length && _stageBaseScales[idx] != Vector3.zero)
                r.transform.localScale = _stageBaseScales[idx] * Mathf.Lerp(0.12f, 1f, p);

            r.enabled = p > 0.02f;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(DissolveID, dissolve);
            _mpb.SetColor(EmissionColorID, _stageEmissive * bloom);
            r.SetPropertyBlock(_mpb);
        }

        private IEnumerator FadePlaqueIn()
        {
            if (_conclusionGroup == null) yield break;
            float start = _conclusionGroup.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _plaqueFadeSeconds);
                _conclusionGroup.alpha = Mathf.Lerp(start, 1f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            _conclusionGroup.alpha = 1f;
        }
    }
}
