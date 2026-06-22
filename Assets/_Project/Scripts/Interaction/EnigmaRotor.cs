// -----------------------------------------------------------------------------
//  EnigmaRotor.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 2)
//
//  One of the three grabbable Enigma rotors. It is a thin wrapper around the
//  reusable XRGrabTwistDisk (which provides the constrained grab-and-twist with
//  26 detents) that adds rotor-specific presentation:
//
//   * Shows the currently selected letter on a small TMP wheel readout.
//   * Publishes EnigmaRotorChangedEvent so the audio/idle-hint systems react.
//   * Lets the EnigmaController push values back in (for stepping animation while
//     the player types, and for reset / demo mode).
//
//  Rotor letters follow A=0 … Z=25, matching EnigmaMachine.
// -----------------------------------------------------------------------------

using System;
using Decrypted.Core;
using TMPro;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class EnigmaRotor : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("0 = leftmost (slowest), 2 = rightmost (fastest).")]
        [SerializeField] private int _rotorIndex = 0;

        [Header("Wiring")]
        [Tooltip("The grab-twist dial driving this rotor.")]
        [SerializeField] private XRGrabTwistDisk _disk;
        [Tooltip("Optional letter readout (shows the rotor's current letter).")]
        [SerializeField] private TMP_Text _letterLabel;

        /// <summary>(rotorIndex, value 0-25) raised whenever this rotor settles.</summary>
        public event Action<int, int> OnValueChanged;

        public int RotorIndex => _rotorIndex;
        public int Value => _disk != null ? _disk.CurrentStep : 0;

        private void OnEnable()
        {
            if (_disk != null) _disk.OnStepChanged += HandleStep;
            UpdateLabel(Value);
        }

        private void OnDisable()
        {
            if (_disk != null) _disk.OnStepChanged -= HandleStep;
        }

        private void HandleStep(int step)
        {
            UpdateLabel(step);
            EventBus.Publish(new EnigmaRotorChangedEvent(_rotorIndex, step));
            OnValueChanged?.Invoke(_rotorIndex, step);
        }

        /// <summary>Set the rotor value, optionally animating the wheel (used when
        /// rotors visibly advance as the player types, and for reset).</summary>
        public void SetValue(int value, bool animate)
        {
            if (_disk == null) return;
            if (animate) _disk.AnimateToStep(value);
            else _disk.SetStep(value, silent: false);
        }

        private void UpdateLabel(int step)
        {
            if (_letterLabel != null)
                _letterLabel.text = ((char)('A' + (((step % 26) + 26) % 26))).ToString();
        }
    }
}
