using CierzoArena.Combat;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>
    /// The minimal set of gameplay orders the prototype currently supports.
    /// Deliberately limited to the existing behaviours (no abilities, items,
    /// hold, patrol or order queues).
    /// </summary>
    public enum UnitOrderType
    {
        Move,
        Attack,
        Stop
    }

    /// <summary>
    /// Explicit, immutable order boundary between intent (player input, tests,
    /// future AI, future networking) and execution (<see cref="UnitOrderController"/>).
    /// It carries only what the current orders need; it is NOT a network DTO and
    /// makes no assumptions about serialization or authority.
    /// </summary>
    public readonly struct UnitOrderCommand
    {
        public UnitOrderType Type { get; }
        public Vector3 Destination { get; }
        public Health Target { get; }

        private UnitOrderCommand(UnitOrderType type, Vector3 destination, Health target)
        {
            Type = type;
            Destination = destination;
            Target = target;
        }

        public static UnitOrderCommand Move(Vector3 destination)
        {
            return new UnitOrderCommand(UnitOrderType.Move, destination, null);
        }

        public static UnitOrderCommand Attack(Health target)
        {
            return new UnitOrderCommand(UnitOrderType.Attack, Vector3.zero, target);
        }

        public static UnitOrderCommand Stop()
        {
            return new UnitOrderCommand(UnitOrderType.Stop, Vector3.zero, null);
        }
    }
}
