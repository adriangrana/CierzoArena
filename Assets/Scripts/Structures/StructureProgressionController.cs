using System.Collections.Generic;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Structures
{
    /// <summary>
    /// Central vulnerability rule for a team's defensive line. Per lane, only the
    /// outer tower starts attackable; destroying it unlocks inner, then gate. A core
    /// unlocks only when all three gates of its team have fallen.
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
                case StructureTier.Core:
                    return AreAllGatesDestroyed(structure.Team);
                default:
                    return false;
            }
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

        private bool AreAllGatesDestroyed(TeamId team)
        {
            bool hasGate = false;
            bool hasTower = false;
            bool allGatesDestroyed = true;
            bool allTowersDestroyed = true;
            for (int i = 0; i < structures.Count; i++)
            {
                StructureEntity candidate = structures[i];
                if (candidate == null || candidate.Team != team || candidate.Kind != StructureKind.Tower)
                {
                    continue;
                }

                hasTower = true;
                if (!candidate.IsDestroyed)
                {
                    allTowersDestroyed = false;
                }

                if (candidate.Tier == StructureTier.Gate)
                {
                    hasGate = true;
                    if (!candidate.IsDestroyed)
                    {
                        allGatesDestroyed = false;
                    }
                }
            }

            // Full arena: the three lane gates are the final prerequisite. Compact
            // spikes without gate tiers use their complete available tower set.
            return hasGate ? allGatesDestroyed : hasTower && allTowersDestroyed;
        }
    }
}
