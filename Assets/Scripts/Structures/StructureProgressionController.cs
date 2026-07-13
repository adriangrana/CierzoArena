using System.Collections.Generic;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Structures
{
    /// <summary>
    /// Central vulnerability rule for a team's defensive line and the single source
    /// of truth for the M23 breach chain. Per lane, only the outer tower starts
    /// attackable; destroying it unlocks inner, then gate. A lane is <b>breached</b>
    /// once all three of its strategic towers are destroyed. Any breached lane makes
    /// the two Core Guard towers vulnerable; the core only opens once both Core
    /// Guards are destroyed. Scenes without authored Core Guards fall back to the
    /// pre-M23 rule (any cleared lane opens the core) so legacy spike scenes keep
    /// working. The server owns the structures, so this evaluation is authoritative.
    /// </summary>
    public sealed class StructureProgressionController : MonoBehaviour
    {
        private static StructureProgressionController active;
        private readonly List<StructureEntity> structures = new List<StructureEntity>();

        public static StructureProgressionController Active => active;

        private void Awake()
        {
            if (active == null)
            {
                active = this;
            }
            else if (active != this)
            {
                Debug.LogWarning("Only one StructureProgressionController should be active in a scene.", this);
            }
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private void OnEnable()
        {
            if (active == null) active = this;
        }

        private void OnDisable()
        {
            if (active == this) active = null;
        }

        public void Register(StructureEntity structure)
        {
            if (structure != null && !structures.Contains(structure))
            {
                structures.Add(structure);
            }
        }

        public void Unregister(StructureEntity structure)
        {
            structures.Remove(structure);
        }

        public bool IsAttackable(StructureEntity structure)
        {
            if (structure == null || structure.IsDestroyed)
            {
                return false;
            }

            switch (structure.Tier)
            {
                case StructureTier.Outer:
                    return true;
                case StructureTier.Inner:
                    return IsTierDestroyed(structure.Team, structure.Lane, StructureTier.Outer);
                case StructureTier.Gate:
                    return IsTierDestroyed(structure.Team, structure.Lane, StructureTier.Inner);
                case StructureTier.CoreGuard:
                    return IsBaseBreached(structure.Team);
                case StructureTier.Core:
                    return IsCoreVulnerable(structure.Team);
                default:
                    return false;
            }
        }

        /// <summary>
        /// A base is breached once every strategic tower (Outer + Inner + Gate) on at
        /// least one lane has fallen. Core Guard towers are excluded (their lane is
        /// None), so they never count toward a breach. This is the M23 BaseBreached
        /// flag consumed by the Core Guards.
        /// </summary>
        public bool IsBaseBreached(TeamId team)
        {
            return IsAnyLaneCleared(team);
        }

        /// <summary>True once at least one lane is breached: the two Core Guard towers
        /// become targetable by heroes and creeps.</summary>
        public bool AreCoreGuardsVulnerable(TeamId team)
        {
            return IsBaseBreached(team);
        }

        /// <summary>
        /// The core opens only when both Core Guard towers are destroyed. If a base
        /// has no authored Core Guards (legacy scenes) the pre-M23 rule applies: any
        /// fully cleared lane opens the core.
        /// </summary>
        public bool IsCoreVulnerable(TeamId team)
        {
            bool hasGuard = false;
            bool allGuardsDestroyed = true;
            for (int i = 0; i < structures.Count; i++)
            {
                StructureEntity candidate = structures[i];
                if (candidate == null || candidate.Team != team || candidate.Kind != StructureKind.Tower || candidate.Tier != StructureTier.CoreGuard)
                {
                    continue;
                }

                hasGuard = true;
                if (!candidate.IsDestroyed)
                {
                    allGuardsDestroyed = false;
                }
            }

            if (hasGuard)
            {
                // Core Guards are protected until BaseBreached, so their destruction
                // already implies a breach occurred: no extra breach check needed.
                return allGuardsDestroyed;
            }

            return IsAnyLaneCleared(team);
        }

        private bool IsTierDestroyed(TeamId team, StructureLane lane, StructureTier tier)
        {
            for (int i = 0; i < structures.Count; i++)
            {
                StructureEntity candidate = structures[i];
                if (candidate != null && candidate.Team == team && candidate.Lane == lane && candidate.Tier == tier)
                {
                    return candidate.IsDestroyed;
                }
            }

            // A missing prerequisite is not an unlock. This protects partially wired
            // scenes and late network spawns from accidentally exposing a core.
            return false;
        }

        /// <summary>
        /// A core opens as soon as every tower belonging to at least one real lane
        /// has fallen. This deliberately evaluates each lane independently: towers
        /// remaining in the other two lanes continue to exist, but no longer make
        /// the core invulnerable after a full breakthrough.
        /// </summary>
        private bool IsAnyLaneCleared(TeamId team)
        {
            foreach (StructureLane lane in new[] { StructureLane.Top, StructureLane.Mid, StructureLane.Bottom })
            {
                bool hasTower = false;
                bool allTowersDestroyed = true;
                for (int i = 0; i < structures.Count; i++)
                {
                    StructureEntity candidate = structures[i];
                    if (candidate == null || candidate.Team != team || candidate.Kind != StructureKind.Tower || candidate.Lane != lane)
                    {
                        continue;
                    }

                    hasTower = true;
                    if (!candidate.IsDestroyed)
                    {
                        allTowersDestroyed = false;
                    }
                }

                if (hasTower && allTowersDestroyed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
