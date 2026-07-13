using System;
using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Environment
{
    /// <summary>
    /// Measured presentation and navigation data for one arched bridge. The visual
    /// prefab remains presentation-only; the associated gameplay deck samples its
    /// walkable crown so agents have a continuous, matching NavMesh surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BridgeVisualProfile : MonoBehaviour
    {
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Quaternion localRotation = Quaternion.identity;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private Bounds visualBounds;
        [SerializeField] private float width;
        [SerializeField] private float length;
        [SerializeField] private float[] normalizedSamples = Array.Empty<float>();
        [SerializeField] private float[] visualSurfaceHeights = Array.Empty<float>();
        [SerializeField] private float[] gameplaySurfaceHeights = Array.Empty<float>();
        [SerializeField] private Vector3 deckCenter;
        [SerializeField] private Vector3 deckForward = Vector3.forward;

        public Bounds VisualBounds => visualBounds;
        public float Width => width;
        public float Length => length;
        public int SampleCount => normalizedSamples?.Length ?? 0;
        public int SegmentCount => Mathf.Max(0, SampleCount - 1);
        public Vector3 DeckCenter => deckCenter;
        public Vector3 DeckForward => deckForward;
        public IReadOnlyList<float> NormalizedSamples => normalizedSamples;
        public IReadOnlyList<float> VisualSurfaceHeights => visualSurfaceHeights;
        public IReadOnlyList<float> GameplaySurfaceHeights => gameplaySurfaceHeights;

        public float EntryHeight => SampleCount > 0 ? gameplaySurfaceHeights[0] : 0f;
        public float ExitHeight => SampleCount > 0 ? gameplaySurfaceHeights[SampleCount - 1] : 0f;
        public float CrownHeight => EvaluateGameplayHeight(.5f);
        public float MaximumVisualDifference
        {
            get
            {
                float maximum = 0f;
                int count = Mathf.Min(visualSurfaceHeights?.Length ?? 0, gameplaySurfaceHeights?.Length ?? 0);
                for (int i = 0; i < count; i++) maximum = Mathf.Max(maximum, Mathf.Abs(visualSurfaceHeights[i] - gameplaySurfaceHeights[i]));
                return maximum;
            }
        }

        /// <summary>Called by the deterministic M23 builder after ray-sampling the
        /// rendered bridge deck. Heights are world heights, as the gameplay deck is
        /// rooted at y=0 to meet the rest of the arena NavMesh.</summary>
        public void ConfigureArc(Bounds bounds, float deckWidth, float deckLength, Vector3 center, Vector3 forward,
            float[] samples, float[] visualHeights, float[] gameplayHeights)
        {
            visualBounds = bounds;
            width = deckWidth;
            length = deckLength;
            deckCenter = center;
            deckForward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.forward;
            normalizedSamples = samples ?? Array.Empty<float>();
            visualSurfaceHeights = visualHeights ?? Array.Empty<float>();
            gameplaySurfaceHeights = gameplayHeights ?? Array.Empty<float>();
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            localScale = transform.localScale;
        }

        public float EvaluateVisualHeight(float normalizedDistance) => Evaluate(normalizedDistance, visualSurfaceHeights);
        public float EvaluateGameplayHeight(float normalizedDistance) => Evaluate(normalizedDistance, gameplaySurfaceHeights);

        private float Evaluate(float normalizedDistance, float[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            if (values.Length == 1 || normalizedSamples == null || normalizedSamples.Length != values.Length) return values[0];
            float sample = Mathf.Clamp01(normalizedDistance);
            for (int i = 1; i < normalizedSamples.Length; i++)
            {
                if (sample > normalizedSamples[i]) continue;
                float range = Mathf.Max(.0001f, normalizedSamples[i] - normalizedSamples[i - 1]);
                return Mathf.Lerp(values[i - 1], values[i], (sample - normalizedSamples[i - 1]) / range);
            }
            return values[values.Length - 1];
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (SampleCount < 2) return;
            Vector3 right = Vector3.Cross(Vector3.up, deckForward).normalized;
            for (int i = 0; i < SampleCount; i++)
            {
                float t = normalizedSamples[i];
                Vector3 horizontal = deckCenter + deckForward * ((t - .5f) * length);
                float visualY = visualSurfaceHeights[i];
                float gameplayY = gameplaySurfaceHeights[i];
                bool aligned = Mathf.Abs(visualY - gameplayY) <= .12f;
                Gizmos.color = aligned ? Color.green : Color.red;
                Vector3 gameplay = new Vector3(horizontal.x, gameplayY, horizontal.z);
                Gizmos.DrawWireSphere(gameplay, .22f);
                Gizmos.DrawLine(gameplay - right * width * .5f, gameplay + right * width * .5f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(gameplay, new Vector3(horizontal.x, visualY, horizontal.z));
            }
        }
#endif
    }
}
