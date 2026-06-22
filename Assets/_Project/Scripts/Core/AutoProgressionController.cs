// -----------------------------------------------------------------------------
//  AutoProgressionController.cs
//  DECRYPTED - A Walk Through the History of Secret Writing
//
//  Automatic room progression. This is the SINGLE advance path used by BOTH normal
//  play and Demo Mode: when a room's win condition fires on the event bus
//  (ExhibitSolvedEvent for the puzzle/reveal rooms, a short dwell for the Atrium),
//  it shows a brief world-space countdown popup and then advances the museum via
//  GameManager.Advance() -> SceneController's existing ScreenFader transition.
//
//  No hand interaction, no door grab, no button press gates progression. If an
//  exit door is present in the room it is auto-opened as a purely cosmetic flourish
//  synced to the countdown - it never blocks or gates the advance.
//
//  Splash is the only deliberate gate: its PLAY button raises ExperienceStartedEvent
//  (handled by GameManager) - the menu's natural start, not a win-condition advance.
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using Decrypted.Interaction;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Decrypted.Core
{
    [DisallowMultipleComponent]
    public class AutoProgressionController : MonoBehaviour
    {
        [Header("Countdown")]
        [Tooltip("Seconds the countdown popup shows before advancing.")]
        [SerializeField] private int _countdownSeconds = 5;
        [Tooltip("Metres in front of the player the popup spawns.")]
        [SerializeField] private float _spawnDistance = 1.7f;

        [Header("Non-puzzle rooms")]
        [Tooltip("Seconds to take in the Atrium before the countdown starts.")]
        [SerializeField] private float _atriumDwell = 6f;

        [Header("Fonts (match the world-label look)")]
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;

        // Rooms that progress via a win condition (ExhibitSolvedEvent).
        private static readonly HashSet<MuseumState> _winRooms = new HashSet<MuseumState>
        {
            MuseumState.AncientRoom, MuseumState.WWIIRoom, MuseumState.VaultRoom, MuseumState.RevealChamber
        };

        private Camera _head;
        private CountdownPanel _active;
        private bool _busy;

        private Camera Head
        {
            get
            {
                if (_head == null)
                {
                    var o = FindObjectOfType<XROrigin>();
                    _head = (o != null && o.Camera != null) ? o.Camera : Camera.main;
                }
                return _head;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ExhibitSolvedEvent>(OnExhibitSolved);
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExhibitSolvedEvent>(OnExhibitSolved);
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
        }

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            _busy = false;                              // new room, ready to progress again
            if (e.Room == MuseumState.Atrium)
                StartCoroutine(DwellThenCountdown(MuseumState.Atrium, _atriumDwell, "This way"));
        }

        private void OnExhibitSolved(ExhibitSolvedEvent e)
        {
            if (!_winRooms.Contains(e.Room)) return;
            string headline = e.Room == MuseumState.RevealChamber ? "The story is complete" : "Exhibit complete";
            BeginCountdown(e.Room, headline);
        }

        private IEnumerator DwellThenCountdown(MuseumState room, float dwell, string headline)
        {
            float t = 0f;
            while (t < dwell)
            {
                if (GameManager.Instance == null || GameManager.Instance.CurrentState != room) yield break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            BeginCountdown(room, headline);
        }

        private void BeginCountdown(MuseumState room, string headline)
        {
            if (_busy) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != room) return;
            _busy = true;

            // Cosmetic flourish: auto-open this room's exit door if one exists.
            foreach (var d in FindObjectsOfType<RoomDoor>(true))
                if (d.Room == room) d.PlayExitFlourish();

            var cam = Head;
            Vector3 pos;
            Transform headT = cam != null ? cam.transform : null;
            if (headT != null)
            {
                Vector3 fwd = headT.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();
                pos = headT.position + fwd * _spawnDistance;
                pos.y = headT.position.y;               // head height at this moment
            }
            else pos = transform.position + Vector3.forward * _spawnDistance;

            var go = new GameObject("CountdownPanel");
            _active = go.AddComponent<CountdownPanel>();
            _active.Show(headline, _countdownSeconds, pos, headT, _titleFont, _bodyFont, () =>
            {
                _active = null;
                if (GameManager.Instance != null) GameManager.Instance.Advance(); // ScreenFader transition + advance
            });
        }
    }
}
