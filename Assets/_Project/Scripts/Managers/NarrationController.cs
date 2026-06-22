// -----------------------------------------------------------------------------
//  NarrationController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A museum "audio-guide" narrator. It listens to the same flow events every
//  other system uses (RoomEnteredEvent, ExhibitSolvedEvent) and, at each beat,
//  (1) shows the narration line as an on-screen caption via ShowHintEvent (the
//  UIManager comfort-toast), and (2) plays a voice-over audio key through the
//  existing AudioManager. The voice keys (vo_*) are placeholders until a voice
//  actor records them — captions work immediately, audio is opt-in via
//  _voiceEnabled so the demo never spams "clip not found" before the VO exists.
//
//  It is deliberately passive: it never advances rooms or solves puzzles, so it
//  can run alongside Demo Mode without affecting its timing. Attach to the
//  Managers object next to the other manager scripts.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Decrypted.Core;
using UnityEngine;

namespace Decrypted.Managers
{
    [DisallowMultipleComponent]
    public class NarrationController : MonoBehaviour
    {
        [Header("Channels")]
        [Tooltip("Show the narration line as an on-screen caption (UIManager toast).")]
        [SerializeField] private bool _captionsEnabled = true;
        [Tooltip("Play the vo_* voice-over clip via AudioManager. Leave OFF until " +
                 "real voice clips are added to the AudioManager library (avoids " +
                 "'clip not found' warnings with placeholder keys).")]
        [SerializeField] private bool _voiceEnabled = false;

        [Header("Timing")]
        [Tooltip("Seconds a room-intro caption stays on screen.")]
        [SerializeField] private float _introCaptionSeconds = 7f;
        [Tooltip("Seconds a puzzle-solved caption stays on screen.")]
        [SerializeField] private float _solveCaptionSeconds = 5f;
        [Tooltip("Small delay after entering a room before the narrator speaks, so " +
                 "the fade/transition settles first.")]
        [SerializeField] private float _introDelay = 0.8f;

        // Room-entry narration. Keyed by MuseumState.
        private static readonly Dictionary<MuseumState, string> Intro = new()
        {
            { MuseumState.Splash,
                "Welcome to DECRYPTED, a walk through the history of secret writing. " +
                "For three thousand years, the art of hiding a message has shaped the fate of empires." },
            { MuseumState.Atrium,
                "Ahead lie four chapters in the story of cryptography. Each exhibit holds a puzzle. " +
                "Decode it, and the museum carries you onward." },
            { MuseumState.AncientRoom,
                "Ancient Rome. Julius Caesar guarded his orders with a simple, powerful idea: " +
                "shift every letter of the alphabet by three. Turn the disk to reveal his message." },
            { MuseumState.WWIIRoom,
                "1942. The German military believes its Enigma machine writes unbreakable code. " +
                "Three rotors hold the key. Set them to M-A-C and decode the intercepted signal." },
            { MuseumState.VaultRoom,
                "Today, encryption protects every transaction, every message, every secret. " +
                "The cipher you just broke is the key. Enter the passphrase to open the vault." },
            { MuseumState.RevealChamber,
                "From Caesar's shifted alphabet to modern digital security, the principle has never changed: " +
                "transform meaning into a secret only the intended recipient can reveal." },
        };

        // Puzzle-solved stingers. Keyed by the solved room.
        private static readonly Dictionary<MuseumState, string> Solved = new()
        {
            { MuseumState.AncientRoom,
                "Decoded: CROSS THE RUBICON. Caesar's most famous order. The die is cast." },
            { MuseumState.WWIIRoom,
                "Decoded: VICTORY. The signal is broken, and with it, the tide of the war." },
            { MuseumState.VaultRoom,
                "Access granted. A secret is only ever as safe as the key that guards it." },
        };

        private bool _splashSpoken;

        private void OnEnable()
        {
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Subscribe<ExhibitSolvedEvent>(OnExhibitSolved);
            // Splash is set instantly at boot (no RoomEnteredEvent fires for it),
            // so we catch it via StateChangedEvent to play the welcome line.
            EventBus.Subscribe<StateChangedEvent>(OnStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Unsubscribe<ExhibitSolvedEvent>(OnExhibitSolved);
            EventBus.Unsubscribe<StateChangedEvent>(OnStateChanged);
        }

        private void OnStateChanged(StateChangedEvent e)
        {
            if (e.Current == MuseumState.Splash && !_splashSpoken && Intro.TryGetValue(MuseumState.Splash, out var line))
            {
                _splashSpoken = true;
                StartCoroutine(SpeakAfter(_introDelay, line, _introCaptionSeconds, "vo_intro_Splash"));
            }
        }

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            if (Intro.TryGetValue(e.Room, out var line))
                StartCoroutine(SpeakAfter(_introDelay, line, _introCaptionSeconds, "vo_intro_" + e.Room));
        }

        private void OnExhibitSolved(ExhibitSolvedEvent e)
        {
            if (Solved.TryGetValue(e.Room, out var line))
                Speak(line, _solveCaptionSeconds, "vo_solved_" + e.Room);
        }

        private System.Collections.IEnumerator SpeakAfter(float delay, string line, float hold, string voKey)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay); // unscaled: survives a time freeze
            Speak(line, hold, voKey);
        }

        private void Speak(string line, float hold, string voKey)
        {
            if (_captionsEnabled)
                EventBus.Publish(new ShowHintEvent(line, hold));

            if (_voiceEnabled && AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(voKey, 1f);
        }
    }
}
