using System;
using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>
    /// Optional presentation data for a hero archetype. Gameplay remains defined by
    /// <see cref="HeroDefinition"/>; this profile only decides whether a visual prefab
    /// replaces the common placeholder mesh at runtime.
    /// </summary>
    [Serializable]
    public sealed class HeroVisualDefinition
    {
        [SerializeField] private string heroId;
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localRotation;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private bool hidePlaceholder = true;
        [SerializeField] private bool hideTeamIndicator = true;
        [SerializeField] private float groundOffset;

        public string HeroId => heroId;
        public GameObject VisualPrefab => visualPrefab;
        public Vector3 LocalPosition => localPosition + Vector3.up * groundOffset;
        public Quaternion LocalRotation => Quaternion.Euler(localRotation);
        public Vector3 LocalScale => localScale;
        public bool HidePlaceholder => hidePlaceholder;
        public bool HideTeamIndicator => hideTeamIndicator;
        public float GroundOffset => groundOffset;
        public bool HasVisual => visualPrefab != null;

        public void Configure(string id, GameObject prefab, Vector3 position, Vector3 rotation, Vector3 scale, bool hide, float offset = 0f, bool hideIndicator = true)
        {
            heroId = id;
            visualPrefab = prefab;
            localPosition = position;
            localRotation = rotation;
            localScale = scale == Vector3.zero ? Vector3.one : scale;
            hidePlaceholder = hide;
            groundOffset = offset;
            hideTeamIndicator = hideIndicator;
        }
    }

    /// <summary>
    /// Runtime catalogue of optional hero visuals. It is deliberately separate from
    /// <see cref="HeroDefinition"/> because the development roster is created in
    /// memory, while visual prefab references must stay as authored asset references.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroVisualCatalog", menuName = "Cierzo Arena/Hero Visual Catalog")]
    public sealed class HeroVisualCatalog : ScriptableObject
    {
        [SerializeField] private HeroVisualDefinition[] visuals = Array.Empty<HeroVisualDefinition>();

        private readonly Dictionary<string, HeroVisualDefinition> byHeroId = new(StringComparer.Ordinal);
        private bool indexed;
        private static HeroVisualCatalog shared;

        public static HeroVisualCatalog Shared
        {
            get
            {
                if (shared != null) return shared;
                shared = Resources.Load<HeroVisualCatalog>("Heroes/HeroVisualCatalog");
                if (shared != null) shared.Reindex();
                return shared;
            }
        }

        public IReadOnlyList<HeroVisualDefinition> Visuals
        {
            get { Reindex(); return visuals; }
        }

        public bool TryGet(string heroId, out HeroVisualDefinition visual)
        {
            visual = null;
            Reindex();
            return !string.IsNullOrWhiteSpace(heroId) && byHeroId.TryGetValue(heroId, out visual) && visual != null;
        }

        public HeroVisualDefinition Resolve(string heroId)
        {
            return TryGet(heroId, out HeroVisualDefinition visual) ? visual : null;
        }

        /// <summary>Editor/test configuration entry point. Runtime only consumes the
        /// serialized data already loaded through this catalogue.</summary>
        public void SetVisuals(HeroVisualDefinition[] entries)
        {
            visuals = entries ?? Array.Empty<HeroVisualDefinition>();
            indexed = false;
            Reindex();
        }

        private void OnValidate()
        {
            indexed = false;
        }

        private void Reindex()
        {
            if (indexed) return;
            byHeroId.Clear();
            foreach (HeroVisualDefinition visual in visuals)
            {
                if (visual == null || string.IsNullOrWhiteSpace(visual.HeroId) || byHeroId.ContainsKey(visual.HeroId)) continue;
                byHeroId.Add(visual.HeroId, visual);
            }
            indexed = true;
        }
    }
}