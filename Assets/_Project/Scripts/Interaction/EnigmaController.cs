// -----------------------------------------------------------------------------
//  EnigmaController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 2, brain)
//
//  Orchestrates the whole Enigma exhibit. It wires together the three rotors, the
//  keyboard, the lampboard, the signal-trace wiring and the commit lever around
//  the EnigmaMachine cipher core, and owns the win logic and the machine's
//  dramatic "power-up" once the player decrypts the scripted message.
//
//  TEACHING MODEL — "live recomputation"
//  -------------------------------------
//  We keep the exact string of letters the player has typed (_typed). On every
//  keypress (or whenever a rotor is turned) we recompute the FULL decode from the
//  current rotor key using EnigmaMachine.Transform(), which is a *pure* function
//  that never mutates the live machine. The lampboard then lights the last
//  decoded letter.
//
//  Crucially the three dials DO NOT auto-advance while typing — they stay on the
//  chosen key so the player can see "key = MAC" the whole time, while the cipher
//  core internally steps its rotor odometer per character (that's what makes the
//  same letter light different lamps). This keeps the *interface* honest and
//  legible while preserving the real lesson: stepping rotors => changing mapping.
//
//  WIN CONDITION
//  -------------
//  The exhibit is ready to commit when (a) the rotors equal the solution key and
//  (b) the running decode ends with the plaintext word. At that point the brass
//  lever arms; pulling it commits the decryption and powers the machine up:
//  the historical timeline wall illuminates, the wiring begins to pulse
//  continuously, the exit door opens, a success chime plays, and the room is
//  marked solved (which the GameManager turns into the next transition).
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Managers;
using Decrypted.Visuals;
using TMPro;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class EnigmaController : MonoBehaviour
    {
        [Header("Cipher core")]
        [SerializeField] private EnigmaMachine _machine;

        [Header("Inputs")]
        [Tooltip("The three rotors. Order does not matter; each reports its own index.")]
        [SerializeField] private EnigmaRotor[] _rotors = new EnigmaRotor[3];
        [SerializeField] private EnigmaKeyboard _keyboard;
        [SerializeField] private EnigmaLampboard _lampboard;
        [SerializeField] private EnigmaLeverPull _lever;

        [Header("Visualisation")]
        [Tooltip("Wiring traces that pulse on each keypress and loop when powered.")]
        [SerializeField] private SignalTraceRenderer[] _traces;
        [Tooltip("Optional running decoded read-out (TextMeshPro).")]
        [SerializeField] private TMP_Text _decodedReadout;
        [Tooltip("Optional reference plaque showing the encoded message + suggested key.")]
        [SerializeField] private TMP_Text _referenceReadout;

        [Header("Power-up payoff")]
        [Tooltip("Timeline-wall renderers that fade to emissive when the machine powers up.")]
        [SerializeField] private Renderer[] _timelineWall;
        [SerializeField] private Color _timelineEmissive = new Color(0.95f, 0.78f, 0.35f, 1f);
        [SerializeField] private float _timelinePeak = 1.6f;
        [SerializeField] private float _powerUpSeconds = 1.4f;
        [Tooltip("Optional Animator on the exit door (trigger 'Open' is fired).")]
        [SerializeField] private Animator _exitDoorAnimator;
        [Tooltip("Fallback door transform slid from closed to open if no Animator.")]
        [SerializeField] private Transform _exitDoor;
        [SerializeField] private Vector3 _doorOpenLocalOffset = new Vector3(0f, 3.2f, 0f);
        [SerializeField] private float _doorOpenSeconds = 1.6f;
        [Tooltip("When true, drive the exit door via a Blender FBX Animator (the " +
                 "'Open' trigger) instead of the transform slide. Default false = " +
                 "current behaviour. See Documentation/08_Animation_Pipeline.md.")]
        [SerializeField] private bool _useBlenderAnimations = false;

        [Header("Audio")]
        [SerializeField] private string _gearStepKey = "sfx_gear_step";
        [SerializeField] private string _successKey = "sfx_success_chime";

        // ---- runtime ---------------------------------------------------------

        private readonly System.Text.StringBuilder _typed = new System.Text.StringBuilder(16);
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private bool _armed;          // lever is live (ready to commit)
        private bool _solved;
        private string _lastDecoded = string.Empty;

        // Exit-door motion goes through this swappable animator (procedural slide or
        // Blender Animator), so this controller never moves the door transform directly.
        private IEnigmaAnimator _enigmaAnim;
        private IEnigmaAnimator EnigAnim => _enigmaAnim ??= (_useBlenderAnimations && _exitDoorAnimator != null)
            ? (IEnigmaAnimator)new BlenderEnigmaAnimator(_exitDoorAnimator)
            : new ProceduralEnigmaAnimator(_exitDoor);

        // ----------------------------------------------------------------- life

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_machine == null) _machine = GetComponentInChildren<EnigmaMachine>();
            SetTimelineEmissive(0f);
            RefreshReference();
        }

        private void OnEnable()
        {
            if (_keyboard != null)
            {
                _keyboard.OnKeyPressed += HandleKey;
                _keyboard.OnClear += HandleClear;
            }
            foreach (var r in _rotors) if (r != null) r.OnValueChanged += HandleRotor;
            if (_lever != null) { _lever.OnPulled += HandleLever; _lever.Armed = false; }

            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        private void OnDisable()
        {
            if (_keyboard != null)
            {
                _keyboard.OnKeyPressed -= HandleKey;
                _keyboard.OnClear -= HandleClear;
            }
            foreach (var r in _rotors) if (r != null) r.OnValueChanged -= HandleRotor;
            if (_lever != null) _lever.OnPulled -= HandleLever;

            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        // ------------------------------------------------------------- wiring in

        private void HandleKey(char input)
        {
            if (_solved) return;

            _typed.Append(input);
            Recompute(lightLast: true);

            // Per-keypress mechanical flourish: a gear step plus a signal pulse
            // racing through the wiring to the lamp. (The key-clack itself is
            // played by the keyboard; the lamp sound by the lampboard.)
            if (!string.IsNullOrEmpty(_gearStepKey) && AudioManager.Instance != null)
                AudioManager.Instance.Play(_gearStepKey, transform.position, true, 0.5f,
                    Random.Range(0.97f, 1.04f));
            EmitTraces();

            char output = _lastDecoded.Length > 0 ? _lastDecoded[_lastDecoded.Length - 1] : input;
            EventBus.Publish(new EnigmaKeyPressedEvent(input, output));
        }

        private void HandleClear()
        {
            if (_solved) return;
            _typed.Clear();
            _lastDecoded = string.Empty;
            if (_lampboard != null) _lampboard.AllOff();
            Recompute(lightLast: false);
        }

        private void HandleRotor(int index, int value)
        {
            if (_solved) return;
            // Turning a dial re-keys the machine; the running decode changes live.
            Recompute(lightLast: false);
        }

        private void HandleLever()
        {
            if (_solved) return;
            if (_armed) StartCoroutine(PowerUp());
            else
            {
                // Not ready — nudge the player toward the goal without spoiling it.
                EventBus.Publish(new ShowHintEvent(
                    "Set the rotors to the suggested key, then type the coded message.", 3.5f));
            }
        }

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            // Give the exhibit a clean slate each time the player enters the room
            // (also covers replays after a full reset / demo loops).
            if (e.Room == MuseumState.WWIIRoom && !_solved)
            {
                _typed.Clear();
                _lastDecoded = string.Empty;
                if (_lampboard != null) _lampboard.AllOff();
                if (_keyboard != null) _keyboard.SetAccepting(true);
                Recompute(lightLast: false);
            }
        }

        // ------------------------------------------------------- core recompute

        /// <summary>Recompute the full decode from the current rotor key. Pure with
        /// respect to the cipher core (never mutates live machine state).</summary>
        private void Recompute(bool lightLast)
        {
            int r0 = RotorValue(0), r1 = RotorValue(1), r2 = RotorValue(2);
            string source = _typed.ToString();
            _lastDecoded = _machine != null ? _machine.Transform(source, r0, r1, r2) : source;

            if (_decodedReadout != null) _decodedReadout.text = _lastDecoded;

            if (lightLast && _lampboard != null && _lastDecoded.Length > 0)
                _lampboard.Light(_lastDecoded[_lastDecoded.Length - 1]);

            EvaluateReadiness(r0, r1, r2);
        }

        private void EvaluateReadiness(int r0, int r1, int r2)
        {
            if (_machine == null) return;
            bool wasArmed = _armed;
            _armed = KeyMatches(r0, r1, r2) &&
                     _lastDecoded.Length > 0 &&
                     _lastDecoded.EndsWith(_machine.PlaintextWord, System.StringComparison.Ordinal);

            if (_lever != null) _lever.Armed = _armed;

            // Announce the commit beat exactly once, when we first become ready.
            if (_armed && !wasArmed)
                EventBus.Publish(new ShowHintEvent("Decryption ready. Pull the lever.", 4f));
        }

        private bool KeyMatches(int r0, int r1, int r2)
        {
            if (_machine == null) return false;
            string key = _machine.SolutionKey;
            if (key.Length < 3) return false;
            return r0 == key[0] - 'A' && r1 == key[1] - 'A' && r2 == key[2] - 'A';
        }

        private int RotorValue(int index)
        {
            foreach (var r in _rotors) if (r != null && r.RotorIndex == index) return r.Value;
            return 0;
        }

        // --------------------------------------------------------- power-up FX

        private IEnumerator PowerUp()
        {
            _solved = true;
            _armed = false;
            if (_keyboard != null) _keyboard.SetAccepting(false);

            // Wiring goes from one-shot pulses to a continuous powered loop.
            if (_traces != null) foreach (var trace in _traces) if (trace != null) trace.SetPowered(true);

            if (!string.IsNullOrEmpty(_successKey) && AudioManager.Instance != null)
                AudioManager.Instance.Play(_successKey, transform.position, true, 1f);

            // Celebratory lamp shimmer across the board.
            StartCoroutine(LampShimmer());

            // Ramp the timeline wall up to emissive.
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _powerUpSeconds);
                SetTimelineEmissive(Mathf.SmoothStep(0f, 1f, t) * _timelinePeak);
                yield return null;
            }
            SetTimelineEmissive(_timelinePeak);

            // Open the exit door through the animator (Blender trigger wins if used;
            // otherwise the procedural slide runs).
            if (!EnigAnim.TryTriggerOpen() && _exitDoor != null) yield return SlideDoorOpen();

            // Tell the rest of the museum. GameManager schedules the transition.
            GameManager.Instance?.MarkSolved(MuseumState.WWIIRoom);
        }

        private IEnumerator LampShimmer()
        {
            if (_lampboard == null) yield break;
            // Sweep the alphabet, lighting lamps in a quick wave.
            for (int i = 0; i < 26; i++)
            {
                _lampboard.Light((char)('A' + i));
                yield return new WaitForSeconds(0.03f);
            }
        }

        private IEnumerator SlideDoorOpen()
        {
            Vector3 start = _exitDoor.localPosition;
            Vector3 end = start + _doorOpenLocalOffset;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _doorOpenSeconds);
                EnigAnim.SetExitDoorPosition(Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            EnigAnim.SetExitDoorPosition(end);
        }

        private void EmitTraces()
        {
            if (_traces == null) return;
            foreach (var trace in _traces) if (trace != null) trace.Emit();
        }

        private void SetTimelineEmissive(float intensity)
        {
            if (_timelineWall == null) return;
            foreach (var r in _timelineWall)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorID, _timelineEmissive * Mathf.Max(0f, intensity));
                r.SetPropertyBlock(_mpb);
            }
        }

        private void RefreshReference()
        {
            if (_referenceReadout == null || _machine == null) return;
            _referenceReadout.text =
                $"ENCODED:  {_machine.CipherWord}\nSUGGESTED KEY:  {_machine.SolutionKey}";
        }

        // ----------------------------------------------------------- demo hooks

        /// <summary>Used by the DemoDirector: set the three rotors to the solution
        /// key, then type the ciphertext, then (optionally) pull the lever.</summary>
        public IEnumerator AutoSolve(float perKeyDelay = 0.18f, bool pullLever = true)
        {
            if (_machine == null) yield break;
            // Dial the rotors to the key.
            string key = _machine.SolutionKey;
            for (int i = 0; i < _rotors.Length; i++)
            {
                var r = _rotors[i];
                if (r == null) continue;
                int target = key[Mathf.Clamp(r.RotorIndex, 0, key.Length - 1)] - 'A';
                r.SetValue(target, animate: true);
            }
            yield return new WaitForSeconds(0.6f);

            // Type the ciphertext through the real keypress path.
            foreach (char c in _machine.CipherWord)
            {
                HandleKey(c);
                yield return new WaitForSeconds(perKeyDelay);
            }

            yield return new WaitForSeconds(0.3f);
            if (pullLever && _lever != null) _lever.ForcePull();

            // DEMO GUARANTEE (demo hook only): the recorded walkthrough must always
            // commit and advance. Give the lever its full swing to fire the natural
            // commit path first (OnPulled -> HandleLever -> PowerUp); only if that did
            // not happen - lever interactable absent, or a sim edge case left it
            // un-armed - do we power the machine up directly so the WWII room still
            // completes and MarkSolved fires. This runs only from AutoSolve (Demo
            // Mode); the live player path (HandleLever) is unchanged.
            float wait = 0f;
            while (!_solved && wait < 1.5f) { wait += Time.deltaTime; yield return null; }
            if (!_solved) yield return PowerUp();
        }
    }
}
