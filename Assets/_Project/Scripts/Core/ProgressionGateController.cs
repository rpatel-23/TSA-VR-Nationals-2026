// -----------------------------------------------------------------------------
//  ProgressionGateController.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  The ONLY way the player moves between rooms. Replaces the old auto-advance
//  logic (GameManager auto-advancing on solve, ManualFlowController's timers and
//  auto-solve). When a room's win condition is met it spawns a RoomCompletePanel
//  anchored to the player's CURRENT head pose; the experience only advances when
//  the player physically presses that panel's "Let's Go" button.
//
//  Win conditions per room:
//    Splash / Atrium  : no puzzle, so a short dwell after entering (let the player
//                       look around), then the panel appears.
//    Ancient/WWII/Vault: the puzzle's ExhibitSolvedEvent (unchanged puzzle logic).
//    RevealChamber    : FinalRevealController fires ExhibitSolvedEvent when the
//                       closing morph finishes.
//
//  Inert while Demo Mode is on, so the DemoDirector's self-playing recording path
//  is untouched.
// -----------------------------------------------------------------------------

using System.Collections;
using Decrypted.Interaction;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Decrypted.Core
{
    [DisallowMultipleComponent]
    public class ProgressionGateController : MonoBehaviour
    {
        [Header("Panel placement")]
        [Tooltip("Metres in front of the player the panel spawns (1.5-2.0).")]
        [SerializeField] private float _spawnDistance = 1.6f;

        [Header("Dwell before the panel in non-puzzle rooms (seconds)")]
        [SerializeField] private float _splashDwell = 3f;
        [SerializeField] private float _atriumDwell = 4f;

        [Header("Fonts (optional - falls back to TMP default)")]
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;

        private Camera _head;
        private RoomCompletePanel _active;

        private bool Manual => GameManager.Instance != null && !GameManager.Instance.DemoMode;

        private Camera Head
        {
            get
            {
                if (_head == null)
                {
                    var origin = FindObjectOfType<XROrigin>();
                    _head = (origin != null && origin.Camera != null) ? origin.Camera : Camera.main;
                }
                return _head;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Subscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Unsubscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            ClearPanel();              // never leave a stale panel between rooms
            if (!Manual) return;

            switch (e.Room)
            {
                case MuseumState.Splash:
                    StartCoroutine(DwellThenPanel(_splashDwell, MuseumState.Splash,
                        "Welcome to DECRYPTED", "Ready to begin your tour?"));
                    break;
                case MuseumState.Atrium:
                    StartCoroutine(DwellThenPanel(_atriumDwell, MuseumState.Atrium,
                        "Welcome", "Ready to explore the first exhibit?"));
                    break;
                // Ancient / WWII / Vault / Reveal wait for their ExhibitSolvedEvent.
            }
        }

        private void OnExhibitSolved(ExhibitSolvedEvent e)
        {
            if (!Manual) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != e.Room) return;

            if (e.Room == MuseumState.RevealChamber)
                SpawnPanel("The history is complete", "Ready to finish?");
            else
                SpawnPanel("Good job!", "Ready to move to the next room?");
        }

        private IEnumerator DwellThenPanel(float seconds, MuseumState room, string headline, string prompt)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == room)
                SpawnPanel(headline, prompt);
        }

        private void SpawnPanel(string headline, string prompt)
        {
            ClearPanel();
            var cam = Head;
            if (cam == null) { Debug.LogWarning("[Progression] no XR camera; cannot place panel."); return; }

            // Anchor to the player's CURRENT head pose (viewerPose), not a fixed
            // world point. Flatten the look direction so the panel stands upright,
            // and use the head's own Y so it sits at this player's real eye level.
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = cam.transform.position + fwd * _spawnDistance;
            pos.y = cam.transform.position.y;

            var go = new GameObject("RoomCompletePanel");
            _active = go.AddComponent<RoomCompletePanel>();
            _active.Show(headline, prompt, pos, cam.transform, _titleFont, _bodyFont, OnConfirmed);
        }

        // The player pressed "Let's Go". The panel removes itself; we advance.
        private void OnConfirmed()
        {
            _active = null;
            GameManager.Instance?.Advance();
        }

        private void ClearPanel()
        {
            if (_active != null) { Destroy(_active.gameObject); _active = null; }
        }
    }
}
