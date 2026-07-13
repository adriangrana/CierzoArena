using System.Collections.Generic;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Structures
{
    /// <summary>
    /// Central vulnerability rule for a team's defensive line. Per lane, only the
    /// outer tower starts attackable; destroying it unlocks inner, then gate. A core
    /// unlocks when one complete enemy lane has been cleared, so attackers can win
    /// through any lane rather than having to erase every defensive lane.
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
                    return IsAnyLaneCleared(structure.Team);
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
