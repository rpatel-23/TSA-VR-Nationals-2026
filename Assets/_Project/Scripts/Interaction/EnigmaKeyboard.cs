// -----------------------------------------------------------------------------
//  EnigmaKeyboard.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 2)
//
//  The machine's input keyboard: 26 lettered keys (each a PokeButton, so they
//  work by finger-poke or ray-click) plus an optional CLEAR key. The keyboard is
//  purely an input device — it forwards key letters to the EnigmaController via
//  OnKeyPressed / OnClear and plays the mechanical key-clack. The controller owns
//  the cipher, lamps and win logic.
//
//  Keys can be auto-labelled from their PokeButton.Value, or you can leave Value
//  empty and assign letters in order via the inspector list (A,B,C…).
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Decrypted.Managers;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class EnigmaKeyboard : MonoBehaviour
    {
        [Header("Keys")]
        [Tooltip("The 26 letter keys. Each key's PokeButton.Value should be its letter; " +
                 "if left blank, letters A..Z are assigned in list order.")]
        [SerializeField] private List<PokeButton> _keys = new List<PokeButton>();
        [Tooltip("Optional CLEAR key to reset the current attempt.")]
        [SerializeField] private PokeButton _clearKey;
        [Tooltip("Optional DELETE/backspace key to remove the last typed letter.")]
        [SerializeField] private PokeButton _deleteKey;

        [Header("Audio")]
        [SerializeField] private string _keyClackKey = "sfx_key_clack";

        /// <summary>Raised with the pressed letter (always uppercase A-Z).</summary>
        public event Action<char> OnKeyPressed;
        /// <summary>Raised when the CLEAR key is pressed.</summary>
        public event Action OnClear;
        /// <summary>Raised when the DELETE/backspace key is pressed.</summary>
        public event Action OnDelete;

        private bool _accepting = true;

        private void Awake()
        {
            // Auto-assign letters where Value is blank.
            char letter = 'A';
            foreach (var key in _keys)
            {
                if (key == null) continue;
                if (string.IsNullOrEmpty(key.Value)) { key.Value = letter.ToString(); }
                letter = (char)(letter + 1);
            }
        }

        private void OnEnable()
        {
            foreach (var key in _keys) if (key != null) key.OnPressed += HandleKey;
            if (_clearKey != null) _clearKey.OnPressed += HandleClear;
            if (_deleteKey != null) _deleteKey.OnPressed += HandleDelete;
        }

        private void OnDisable()
        {
            foreach (var key in _keys) if (key != null) key.OnPressed -= HandleKey;
            if (_clearKey != null) _clearKey.OnPressed -= HandleClear;
            if (_deleteKey != null) _deleteKey.OnPressed -= HandleDelete;
        }

        /// <summary>Enable/disable input (e.g. lock once the puzzle is solved).</summary>
        public void SetAccepting(bool accepting) => _accepting = accepting;

        private void HandleKey(string value)
        {
            if (!_accepting || string.IsNullOrEmpty(value)) return;
            char c = char.ToUpperInvariant(value[0]);
            if (c < 'A' || c > 'Z') return;

            if (!string.IsNullOrEmpty(_keyClackKey) && AudioManager.Instance != null)
                AudioManager.Instance.Play(_keyClackKey, transform.position, true, 0.85f,
                    UnityEngine.Random.Range(0.96f, 1.05f)); // slight pitch variance per key

            OnKeyPressed?.Invoke(c);
        }

        private void HandleClear(string _)
        {
            if (!_accepting) return;
            OnClear?.Invoke();
        }

        private void HandleDelete(string _)
        {
            if (!_accepting) return;
            if (!string.IsNullOrEmpty(_keyClackKey) && AudioManager.Instance != null)
                AudioManager.Instance.Play(_keyClackKey, transform.position, true, 0.85f, 0.92f);
            OnDelete?.Invoke();
        }
    }
}
