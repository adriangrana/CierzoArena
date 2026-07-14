using System;
using UnityEngine;

namespace CierzoArena.Core
{
    /// <summary>Small runtime-only bridge from the final-match UI to the owner of
    /// session teardown. It intentionally carries no networking implementation so
    /// the presentation assembly stays independent from the Netcode assembly.</summary>
    public static class MatchEndExitRequest
    {
        public static event Action Requested;

        public static bool Request()
        {
            int receiverCount = Requested?.GetInvocationList().Length ?? 0;
            Debug.Log($"[M24 MatchEnd] Return-to-room requested; receivers={receiverCount}.");
            Requested?.Invoke();
            return receiverCount > 0;
        }
    }
}
