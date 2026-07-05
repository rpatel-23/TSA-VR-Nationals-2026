// -----------------------------------------------------------------------------
//  EventBus.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A tiny, strongly-typed, static publish/subscribe hub. The whole experience is
//  event-driven: exhibits raise events ("Caesar solved", "Enigma powered up"),
//  and managers (Audio, UI, Scene, Performance) subscribe to them. Nothing polls.
//
//  Why a bus instead of UnityEvents wired in the Inspector?
//   * Exhibits don't need hard references to managers (clean dependency graph).
//   * One place to log/trace progression (invaluable for the demo + QA).
//   * Trivial to add new listeners (e.g. an analytics or accessibility layer)
//     without editing the publishers.
//
//  Performance: handlers are stored per-type in a dictionary of delegates. No
//  reflection, no per-publish allocation. Safe to call every frame, though in
//  this project events are discrete and infrequent.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Decrypted.Core
{
    /// <summary>Marker interface so every event is a value-type-ish payload.</summary>
    public interface IGameEvent { }

    public static class EventBus
    {
        // type -> Delegate (a multicast Action<TEvent>)
        private static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>(32);

        [Tooltip("Toggleable from PerformanceManager; logs every published event.")]
        public static bool VerboseLogging = false;

        public static void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null) return;
            var type = typeof(TEvent);
            if (_handlers.TryGetValue(type, out var existing))
                _handlers[type] = Delegate.Combine(existing, handler);
            else
                _handlers[type] = handler;
        }

        public static void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null) return;
            var type = typeof(TEvent);
            if (_handlers.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null) _handlers.Remove(type);
                else _handlers[type] = result;
            }
        }

        public static void Publish<TEvent>(TEvent evt) where TEvent : IGameEvent
        {
            if (VerboseLogging) Debug.Log($"[EventBus] {typeof(TEvent).Name} :: {evt}");
            if (_handlers.TryGetValue(typeof(TEvent), out var d) && d is Action<TEvent> action)
            {
                // Defensive: a listener throwing should not break the chain.
                try { action.Invoke(evt); }
                catch (Exception e) { Debug.LogError($"[EventBus] handler for {typeof(TEvent).Name} threw: {e}"); }
            }
        }
    }
}
