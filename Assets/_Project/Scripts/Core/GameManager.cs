// -----------------------------------------------------------------------------
//  GameManager.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  The single source of truth for "where are we in the museum". Implements an
//  explicit, forward-only finite state machine over MuseumState. Other systems
//  never set state directly — they call AdvanceTo()/Advance()/ResetExperience()
//  and react to StateChangedEvent on the EventBus.
//
//  Responsibilities:
//   * Hold current MuseumState and validate transitions.
//   * Drive the SceneController (room activation + player placement).
//   * Gate progression: an exhibit must be marked solved before the matching
//     "advance" is permitted (so the player can't skip puzzles).
//   * Provide a clean entry point for Demo / Auto-play mode.
//   * Persist progress through the SaveSystem.
//
//  This is intentionally small and readable: the museum is linear, so the FSM is
//  a guarded sequence rather than a graph. Guards live in CanTransition().
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Util;
using UnityEngine;

namespace Decrypted.Core
{
    [DefaultExecutionOrder(-100)] // initialise before exhibits/UI
    public class GameManager : Singleton<GameManager>
    {
        [Header("Wiring")]
        [SerializeField] private SceneController _sceneController;
        [SerializeField] private bool _verboseEventLogging = false;

        [Header("Flow")]
        [Tooltip("If true the player must solve an exhibit before advancing past it.")]
        [SerializeField] private bool _requireSolveToAdvance = true;

        [Tooltip("If true, restores the player to their last room on launch.")]
        [SerializeField] private bool _restoreProgressOnLaunch = false;

        [Header("Demo / Auto-play")]
        [Tooltip("When enabled, the experience plays itself for recording. See DemoDirector.")]
        [SerializeField] private bool _demoMode = false;

        public MuseumState CurrentState { get; private set; } = MuseumState.Boot;
        public bool DemoMode => _demoMode;

        // The canonical forward order. Index lookups keep CanTransition trivial.
        private static readonly MuseumState[] _order =
        {
            MuseumState.Splash,
            MuseumState.Atrium,
            MuseumState.AncientRoom,
            MuseumState.WWIIRoom,
            MuseumState.VaultRoom,
            MuseumState.RevealChamber,
            MuseumState.Complete
        };

        // Rooms whose puzzle must be solved before the player may move on.
        private static readonly HashSet<MuseumState> _gatedRooms = new HashSet<MuseumState>
        {
            MuseumState.AncientRoom, MuseumState.WWIIRoom, MuseumState.VaultRoom
        };

        private readonly HashSet<MuseumState> _solved = new HashSet<MuseumState>();
        private bool _transitioning;

        // ---------------------------------------------------------------- boot

        protected override void OnSingletonAwake()
        {
            EventBus.VerboseLogging = _verboseEventLogging;
            if (_sceneController == null) _sceneController = FindObjectOfType<SceneController>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ExperienceStartedEvent>(OnExperienceStarted);
            EventBus.Subscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExperienceStartedEvent>(OnExperienceStarted);
            EventBus.Unsubscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private IEnumerator Start()
        {
            // One frame so every Awake/OnEnable has run before we drive the scene.
            yield return null;

            if (_restoreProgressOnLaunch && SaveSystem.HasProgress)
            {
                var restored = SaveSystem.LoadFurthestState();
                foreach (var s in SaveSystem.LoadSolvedRooms()) _solved.Add(s);
                SetState(restored, instant: true);
            }
            else
            {
                SetState(MuseumState.Splash, instant: true);
            }

            if (_demoMode)
            {
                // DemoDirector (optional component) listens for this and scripts input.
                var director = FindObjectOfType<DemoDirector>();
                if (director != null) director.BeginAutoPlay(this);
            }
        }

        // --------------------------------------------------------------- public

        /// <summary>Advance one step in the canonical order, honouring solve-gates.</summary>
        public void Advance()
        {
            int idx = System.Array.IndexOf(_order, CurrentState);
            if (idx < 0 || idx >= _order.Length - 1) return;
            AdvanceTo(_order[idx + 1]);
        }

        /// <summary>Request a specific state. Rejected if the transition is illegal.</summary>
        public void AdvanceTo(MuseumState target)
        {
            if (_transitioning)
            {
                Debug.LogWarning($"[GameManager] ignored {target}; mid-transition.");
                return;
            }
            if (!CanTransition(CurrentState, target))
            {
                Debug.LogWarning($"[GameManager] illegal/gated transition {CurrentState} -> {target}.");
                if (_gatedRooms.Contains(CurrentState) && !_solved.Contains(CurrentState))
                    EventBus.Publish(new ShowHintEvent("Complete this exhibit to continue.", 3f));
                return;
            }
            StartCoroutine(TransitionRoutine(target));
        }

        /// <summary>Mark a room's puzzle complete. Usually raised via ExhibitSolvedEvent.</summary>
        public void MarkSolved(MuseumState room)
        {
            if (_solved.Add(room))
            {
                SaveSystem.SaveSolvedRoom(room);
                EventBus.Publish(new ExhibitSolvedEvent(room));
            }
        }

        public bool IsSolved(MuseumState room) => _solved.Contains(room);

        /// <summary>Full reset back to the splash screen (Save/Reset system).</summary>
        public void ResetExperience()
        {
            StopAllCoroutines();
            _solved.Clear();
            SaveSystem.ClearProgress();
            _transitioning = false;
            SetState(MuseumState.Splash, instant: true);
            EventBus.Publish(new ShowHintEvent("Experience reset.", 2f));
        }

        // -------------------------------------------------------------- guards

        private bool CanTransition(MuseumState from, MuseumState to)
        {
            // Reset path always allowed.
            if (to == MuseumState.Splash) return true;

            int fromIdx = System.Array.IndexOf(_order, from);
            int toIdx = System.Array.IndexOf(_order, to);
            if (fromIdx < 0 || toIdx < 0) return false;

            // Only single forward steps are legal in normal play.
            if (toIdx != fromIdx + 1) return false;

            // Solve-gate: leaving a gated room requires its puzzle solved.
            // Demo mode bypasses this so the director can drive the full sequence
            // even when exhibit GameObjects aren't wired up in the scene.
            if (_requireSolveToAdvance && !_demoMode && _gatedRooms.Contains(from) && !_solved.Contains(from))
                return false;

            return true;
        }

        // --------------------------------------------------------- transitions

        private IEnumerator TransitionRoutine(MuseumState target)
        {
            _transitioning = true;

            // Fade out -> activate room -> place player -> fade in is delegated to
            // the SceneController, which owns the screen-fade + room toggling.
            if (_sceneController != null)
                yield return _sceneController.TransitionTo(target);

            SetState(target, instant: false);
            EventBus.Publish(new RoomEnteredEvent(target));

            _transitioning = false;
        }

        private void SetState(MuseumState target, bool instant)
        {
            var previous = CurrentState;
            CurrentState = target;
            SaveSystem.SaveFurthestState(target);

            if (instant && _sceneController != null)
                _sceneController.SnapTo(target); // no fade (used at boot/reset)

            EventBus.Publish(new StateChangedEvent(previous, target));
        }

        // ----------------------------------------------------------- listeners

        private void OnExperienceStarted(ExperienceStartedEvent _)
        {
            // Progression is exclusively the physical RoomDoor system now. The old
            // PLAY-button path (Splash -> Atrium) is intentionally inert: the
            // Splash exit door advances instead. Demo Mode still uses this hook.
            if (_demoMode && CurrentState == MuseumState.Splash) AdvanceTo(MuseumState.Atrium);
        }

        private void OnExhibitSolved(ExhibitSolvedEvent e)
        {
            // Manual play never auto-advances: a physical RoomDoor activates on the
            // win condition and the player opens it to move on. Only Demo Mode (the
            // self-playing recording path) still auto-advances on solve.
            if (!_demoMode) return;
            float delay = _sceneController != null ? _sceneController.GetExitDelay(e.Room) : 2.5f;
            StartCoroutine(AdvanceAfter(delay, e.Room));
        }

        private IEnumerator AdvanceAfter(float delay, MuseumState room)
        {
            yield return new WaitForSeconds(delay);
            if (CurrentState == room) Advance();
        }
    }
}
