using CierzoArena.Combat;
using CierzoArena.Units;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>
    /// Outcome of an authoritative order request. Kept transport-agnostic so the
    /// decision logic can be exercised without a running NetworkManager.
    /// </summary>
    public enum OrderRequestResult
    {
        Accepted,
        RejectedUnauthorizedSender,
        RejectedUnresolvedTarget,
        RejectedByDomain
    }

    /// <summary>
    /// Server-side translator between a network order request and the existing
    /// domain order boundary (<see cref="UnitOrderController"/>). It deliberately
    /// knows nothing about NGO types: the transport layer resolves the sender id
    /// and the target <see cref="Health"/> and hands them here. This keeps the
    /// authoritative decision (authorize sender, translate to
    /// <see cref="UnitOrderCommand"/>, delegate final validation to the domain)
    /// isolated from the network plumbing and unit-testable on its own.
    /// </summary>
    public sealed class AuthoritativeOrderProcessor
    {
        private readonly UnitOrderController orderController;
        private ulong owningClientId;

        public AuthoritativeOrderProcessor(UnitOrderController orderController, ulong owningClientId)
        {
            this.orderController = orderController;
            this.owningClientId = owningClientId;
        }

        public ulong OwningClientId => owningClientId;

        /// <summary>
        /// Re-associates the controlled unit with a different owning client. The
        /// server calls this when it (re)assigns ownership of the unit.
        /// </summary>
        public void SetOwningClient(ulong clientId)
        {
            owningClientId = clientId;
        }

        public OrderRequestResult ProcessMove(ulong senderClientId, Vector3 destination)
        {
            if (!IsAuthorized(senderClientId))
            {
                return OrderRequestResult.RejectedUnauthorizedSender;
            }

            return Delegate(UnitOrderCommand.Move(destination));
        }

        public OrderRequestResult ProcessAttack(ulong senderClientId, Health resolvedTarget)
        {
            if (!IsAuthorized(senderClientId))
            {
                return OrderRequestResult.RejectedUnauthorizedSender;
            }

            if (resolvedTarget == null)
            {
                return OrderRequestResult.RejectedUnresolvedTarget;
            }

            return Delegate(UnitOrderCommand.Attack(resolvedTarget));
        }

        public OrderRequestResult ProcessAttackMove(ulong senderClientId, Vector3 destination)
        {
            if (!IsAuthorized(senderClientId))
            {
                return OrderRequestResult.RejectedUnauthorizedSender;
            }

            return Delegate(UnitOrderCommand.AttackMove(destination));
        }

        public OrderRequestResult ProcessStop(ulong senderClientId)
        {
            if (!IsAuthorized(senderClientId))
            {
                return OrderRequestResult.RejectedUnauthorizedSender;
            }

            return Delegate(UnitOrderCommand.Stop());
        }

        private bool IsAuthorized(ulong senderClientId)
        {
            return senderClientId == owningClientId;
        }

        private OrderRequestResult Delegate(in UnitOrderCommand command)
        {
            // Final validation (alive, enemy target, in-range rules, and the M2.4
            // "dead unit rejects external orders" invariant) still belongs to the
            // single domain boundary; the network layer never duplicates it.
            return orderController.Execute(command)
                ? OrderRequestResult.Accepted
                : OrderRequestResult.RejectedByDomain;
        }
    }
}
