using UnityEngine;

namespace CierzoArena.Structures
{
    /// <summary>Minimal presentation-only victory feedback.</summary>
    [RequireComponent(typeof(MatchStateController))]
    public sealed class MatchVictoryDisplay : MonoBehaviour
    {
        private MatchStateController match;
        private string message;
        private GUIStyle messageStyle;

        private void Awake()
        {
            match = GetComponent<MatchStateController>();
            match.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (match != null)
            {
                match.StateChanged -= OnStateChanged;
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (messageStyle == null)
            {
                messageStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 32,
                    normal = { textColor = Color.white }
                };
            }

            Rect panel = new Rect(Screen.width * 0.5f - 220f, 24f, 440f, 74f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(panel, message, messageStyle);
        }

        private void OnStateChanged(MatchState state)
        {
            message = state == MatchState.AzureVictory ? "Azure Victory" :
                state == MatchState.EmberVictory ? "Ember Victory" : string.Empty;
        }
    }
}
