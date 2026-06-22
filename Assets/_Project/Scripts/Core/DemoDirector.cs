// -----------------------------------------------------------------------------
//  DemoDirector.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Drives a hands-free run of the entire museum for recording the showcase video.
//  When the GameManager boots in Demo Mode it calls BeginAutoPlay(); from there
//  this director listens to the flow events and performs each exhibit's scripted
//  solution at the right moment, with human-paced dwell times so the capture
//  feels like a real visitor rather than a fast-forward.
//
//  It only scripts the PUZZLE INPUT. Room activation, fades and player placement
//  remain the GameManager/SceneController's job, and the cinematic camera path is
//  a separate recording rig (see 06_Storyboard_and_Recording.md). That separation
//  keeps the director small and means the same scripted solves can be triggered
//  from an automated test as easily as from a capture session.
//
//  Sequence:
//    Splash  → press PLAY                                   → Atrium
//    Atrium  → dwell, then advance                          → Ancient Room
//    Ancient → rotate Caesar disk to +3 (CROSS THE RUBICON) → auto-advance
//    WWII    → key MAC, type ZLDFDQO → VICTORY, pull lever   → auto-advance
//    Vault   → type VICTORY, ENTER, door opens              → auto-advance
//    Reveal  → sculpture morphs, conclusion fades, Complete
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Interaction;
using UnityEngine;

namespace Decrypted.Core
{
    [DisallowMultipleComponent]
    public class DemoDirector : MonoBehaviour
    {
        [Header("Exhibit references")]
        [SerializeField] private CaesarCipherController _caesar;
        [SerializeField] private EnigmaController _enigma;
        [SerializeField] private VaultKeypad _vault;
        [SerializeField] private FinalRevealController _reveal;

        [Header("Pacing (seconds)")]
        [Tooltip("Delay on the splash screen before pressing PLAY.")]
        [SerializeField] private float _splashDelay = 2.5f;
        [Tooltip("Time spent taking in the atrium before walking to the first exhibit.")]
        [SerializeField] private float _atriumDwell = 5f;
        [Tooltip("Time to read an exhibit's plaque before starting to solve it.")]
        [SerializeField] private float _readDwell = 4f;
        [Tooltip("Per-keypress cadence while typing on the Enigma / vault.")]
        [SerializeField] private float _typeCadence = 0.2f;

        private GameManager _gm;
        private bool _running;
        private readonly HashSet<MuseumState> _handled = new HashSet<MuseumState>();

        private void OnEnable()
        {
            EventBus.Subscribe<StateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        /// <summary>Entry point invoked by GameManager when Demo Mode is enabled.</summary>
        public void BeginAutoPlay(GameManager gm)
        {
            _gm = gm;
            _running = true;
            AutoResolveReferences();

            // We boot already on the splash screen (set instantly, so no
            // RoomEnteredEvent fires for it) — kick the PLAY press directly.
            if (_gm.CurrentState == MuseumState.Splash)
                StartCoroutine(PressPlayAfterDelay());
        }

        // ------------------------------------------------------------- events

        private void OnStateChanged(StateChangedEvent e)
        {
            if (!_running) return;
            // If we ever land back on Splash (e.g. after a reset) re-arm the run.
            if (e.Current == MuseumState.Splash && !_handled.Contains(MuseumState.Splash))
                StartCoroutine(PressPlayAfterDelay());
        }

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            if (!_running || _handled.Contains(e.Room)) return;
            _handled.Add(e.Room);

            switch (e.Room)
            {
                // Atrium has no puzzle: AutoProgressionController handles its dwell +
                // countdown + advance (the same path normal play uses), so the demo
                // does not advance the Atrium itself.
                case MuseumState.AncientRoom:  StartCoroutine(DoAncient()); break;
                case MuseumState.WWIIRoom:     StartCoroutine(DoWWII()); break;
                case MuseumState.VaultRoom:    StartCoroutine(DoVault()); break;
                case MuseumState.RevealChamber:StartCoroutine(DoReveal()); break;
            }
        }

        // --------------------------------------------------------- room scripts

        private IEnumerator PressPlayAfterDelay()
        {
            _handled.Add(MuseumState.Splash);
            yield return new WaitForSeconds(_splashDelay);
            EventBus.Publish(new ExperienceStartedEvent()); // Splash -> Atrium
        }

        private IEnumerator DoAtrium()
        {
            yield return new WaitForSeconds(_atriumDwell);
            if (_gm != null) _gm.Advance(); // Atrium -> Ancient Room
        }

        private IEnumerator DoAncient()
        {
            yield return new WaitForSeconds(_readDwell);
            if (_caesar != null) _caesar.AutoSolve(); // solve -> GameManager auto-advances
        }

        private IEnumerator DoWWII()
        {
            yield return new WaitForSeconds(_readDwell);
            if (_enigma != null)
                yield return _enigma.AutoSolve(_typeCadence, pullLever: true); // -> auto-advances
        }

        private IEnumerator DoVault()
        {
            yield return new WaitForSeconds(_readDwell);
            if (_vault != null)
                yield return _vault.AutoEnter(_typeCadence); // unlock -> auto-advances
        }

        private IEnumerator DoReveal()
        {
            // The reveal controller auto-starts on RoomEntered; calling BeginReveal
            // here is a harmless idempotent safety in case auto-start is disabled.
            yield return new WaitForSeconds(0.5f);
            if (_reveal != null) _reveal.BeginReveal();
        }

        // ------------------------------------------------------------- helpers

        private void AutoResolveReferences()
        {
            if (_caesar == null) _caesar = FindObjectOfType<CaesarCipherController>(true);
            if (_enigma == null) _enigma = FindObjectOfType<EnigmaController>(true);
            if (_vault == null) _vault = FindObjectOfType<VaultKeypad>(true);
            if (_reveal == null) _reveal = FindObjectOfType<FinalRevealController>(true);
        }
    }
}
