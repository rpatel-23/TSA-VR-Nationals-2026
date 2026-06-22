// -----------------------------------------------------------------------------
//  VaultKeypad.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 3)
//
//  The input surface for the modern "digital security" vault. The player has
//  just decrypted VICTORY on the Enigma machine; here they prove that the secret
//  is only useful to whoever holds it by typing it into the vault's keypad. Get
//  it right and the vault opens; get it wrong and it rejects you and clears.
//
//  This component is pure input + validation glue: it collects letters from the
//  VaultKeypadButtons, maintains the entered string, drives a TextMeshPro
//  read-out, and on ENTER compares against the passphrase (which defaults to the
//  Enigma plaintext, VICTORY). It raises VaultAttemptEvent for telemetry/audio
//  and hands a correct attempt to the VaultController, which owns the door drama.
//
//  The passphrase is deliberately editable AND can be auto-sourced from the
//  EnigmaMachine so the two exhibits can never drift out of sync.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Managers;
using TMPro;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class VaultKeypad : MonoBehaviour
    {
        [Header("Passphrase")]
        [Tooltip("The accepted passphrase. If 'Source From Enigma' is set, this is " +
                 "overwritten at runtime with the Enigma plaintext word.")]
        [SerializeField] private string _passphrase = "VICTORY";
        [Tooltip("Pull the passphrase from the Enigma machine so the exhibits stay in sync.")]
        [SerializeField] private EnigmaMachine _sourceFromEnigma;
        [Tooltip("Max characters the player can enter before it auto-trims.")]
        [SerializeField] private int _maxLength = 12;

        [Header("Keys")]
        [Tooltip("Letter keys. Blank Values are auto-assigned A..Z in list order.")]
        [SerializeField] private List<VaultKeypadButton> _letterKeys = new List<VaultKeypadButton>();
        [SerializeField] private VaultKeypadButton _enterKey;
        [SerializeField] private VaultKeypadButton _clearKey;

        [Header("Read-out")]
        [SerializeField] private TMP_Text _display;
        [SerializeField] private Color _neutralColor = new Color(0.6f, 1f, 1f, 1f);
        [SerializeField] private Color _acceptColor = new Color(0.45f, 1f, 0.55f, 1f);
        [SerializeField] private Color _rejectColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] private string _promptText = "ENTER PASSPHRASE";

        [Header("Wiring")]
        [SerializeField] private VaultController _vault;

        [Header("Audio")]
        [SerializeField] private string _rejectSfxKey = "sfx_brass_click";

        private readonly System.Text.StringBuilder _entered = new System.Text.StringBuilder(16);
        private bool _accepting = true;
        private bool _solved;
        private Coroutine _flash;

        // ----------------------------------------------------------------- life

        private void Awake()
        {
            if (_sourceFromEnigma != null) _passphrase = _sourceFromEnigma.PlaintextWord;
            _passphrase = Normalise(_passphrase);

            // Auto-assign letters to any blank letter key.
            char letter = 'A';
            foreach (var key in _letterKeys)
            {
                if (key == null) continue;
                if (string.IsNullOrEmpty(key.Value)) key.Value = letter.ToString();
                letter = (char)(letter + 1);
            }
            if (_enterKey != null && string.IsNullOrEmpty(_enterKey.Value)) _enterKey.Value = "ENTER";
            if (_clearKey != null && string.IsNullOrEmpty(_clearKey.Value)) _clearKey.Value = "CLEAR";

            RenderDisplay(_neutralColor);
        }

        private void OnEnable()
        {
            foreach (var key in _letterKeys) if (key != null) key.OnPressed += HandlePress;
            if (_enterKey != null) _enterKey.OnPressed += HandlePress;
            if (_clearKey != null) _clearKey.OnPressed += HandlePress;
        }

        private void OnDisable()
        {
            foreach (var key in _letterKeys) if (key != null) key.OnPressed -= HandlePress;
            if (_enterKey != null) _enterKey.OnPressed -= HandlePress;
            if (_clearKey != null) _clearKey.OnPressed -= HandlePress;
        }

        // ------------------------------------------------------------- input

        public void SetAccepting(bool accepting) => _accepting = accepting;

        private void HandlePress(string value)
        {
            if (!_accepting || _solved || string.IsNullOrEmpty(value)) return;

            if (value == "ENTER") { Submit(); return; }
            if (value == "CLEAR") { ClearEntry(); return; }

            // Single letter.
            char c = char.ToUpperInvariant(value[0]);
            if (c < 'A' || c > 'Z') return;
            if (_entered.Length >= _maxLength) _entered.Remove(0, 1); // sliding window
            _entered.Append(c);
            RenderDisplay(_neutralColor);
        }

        private void Submit()
        {
            string attempt = _entered.ToString();
            bool correct = string.Equals(attempt, _passphrase, System.StringComparison.Ordinal);

            EventBus.Publish(new VaultAttemptEvent(correct, attempt));

            if (correct)
            {
                _solved = true;
                _accepting = false;
                RenderDisplay(_acceptColor, "ACCESS GRANTED");
                if (_vault != null) _vault.Unlock();
                else GameManager.Instance?.MarkSolved(MuseumState.VaultRoom);
            }
            else
            {
                if (!string.IsNullOrEmpty(_rejectSfxKey) && AudioManager.Instance != null)
                    AudioManager.Instance.Play(_rejectSfxKey, transform.position, true, 0.9f, 0.7f);
                if (_flash != null) StopCoroutine(_flash);
                _flash = StartCoroutine(RejectFlash());
            }
        }

        private void ClearEntry()
        {
            _entered.Clear();
            RenderDisplay(_neutralColor);
        }

        private IEnumerator RejectFlash()
        {
            RenderDisplay(_rejectColor, "ACCESS DENIED");
            yield return new WaitForSecondsRealtime(1.1f);
            _entered.Clear();
            RenderDisplay(_neutralColor);
            _flash = null;
        }

        // ------------------------------------------------------------- display

        private void RenderDisplay(Color color, string overrideText = null)
        {
            if (_display == null) return;
            _display.color = color;
            if (overrideText != null) { _display.text = overrideText; return; }
            _display.text = _entered.Length == 0 ? _promptText : _entered.ToString();
        }

        private static string Normalise(string s)
        {
            if (string.IsNullOrEmpty(s)) return "VICTORY";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                char up = char.ToUpperInvariant(c);
                if (up >= 'A' && up <= 'Z') sb.Append(up);
            }
            return sb.Length == 0 ? "VICTORY" : sb.ToString();
        }

        // ----------------------------------------------------------- demo hook

        /// <summary>Type the passphrase and submit (used by the demo director).</summary>
        public IEnumerator AutoEnter(float perKeyDelay = 0.16f)
        {
            ClearEntry();
            foreach (char c in _passphrase)
            {
                HandlePress(c.ToString());
                yield return new WaitForSecondsRealtime(perKeyDelay);
            }
            yield return new WaitForSecondsRealtime(0.25f);
            Submit();
        }
    }
}
