using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(BasicAttack))]
    [RequireComponent(typeof(ClickMover))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(TeamMember))]
    public sealed class UnitOrderController : MonoBehaviour
    {
        private BasicAttack basicAttack;
        private ClickMover mover;
        private Health health;
        private Health attackTarget;

        private void Awake()
        {
            basicAttack = GetComponent<BasicAttack>();
            mover = GetComponent<ClickMover>();
            health = GetComponent<Health>();
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (attackTarget == null)
            {
                return;
            }

            if (!CanReceiveOrders || !basicAttack.CanAttack(attackTarget))
            {
                CancelAttack();
                return;
            }

            basicAttack.SetTarget(attackTarget);
            basicAttack.Simulate(Time.deltaTime);
            if (!basicAttack.NeedsApproach)
            {
                mover.Stop();
                return;
            }

            mover.MoveTo(basicAttack.GetApproachPosition(attackTarget));
        }

        public bool IssueMove(Vector3 destination)
        {
            return Execute(UnitOrderCommand.Move(destination));
        }

        public bool IssueAttack(Health target)
        {
            return Execute(UnitOrderCommand.Attack(target));
        }

        public void Stop()
        {
            Execute(UnitOrderCommand.Stop());
        }

        /// <summary>
        /// Single reception boundary for gameplay orders. All order acceptance is
        /// validated here (and only here); accepted orders are applied to the
        /// runtime components. Continuous simulation of an accepted order (chasing,
        /// range checks, firing) happens in <see cref="Update"/>, not per command.
        /// </summary>
        public bool Execute(in UnitOrderCommand command)
        {
            if (!CanAccept(command))
            {
                return false;
            }

            switch (command.Type)
            {
                case UnitOrderType.Move:
                    attackTarget = null;
                    basicAttack.ClearTarget();
                    mover.MoveTo(command.Destination);
                    return true;
                case UnitOrderType.Attack:
                    basicAttack.SetTarget(command.Target);
                    attackTarget = command.Target;
                    return true;
                case UnitOrderType.Stop:
                    attackTarget = null;
                    basicAttack.ClearTarget();
                    mover.Stop();
                    return true;
                default:
                    return false;
            }
        }

        public bool CanAccept(in UnitOrderCommand command)
        {
            switch (command.Type)
            {
                case UnitOrderType.Move:
                    return CanReceiveOrders;
                case UnitOrderType.Attack:
                    return CanReceiveOrders && basicAttack.CanAttack(command.Target);
                case UnitOrderType.Stop:
                    return CanReceiveOrders;
                default:
                    return false;
            }
        }

        private bool CanReceiveOrders => health != null && health.IsAlive &&
            (!TryGetComponent(out HeroLifeCycle heroLife) || heroLife.IsAliveForGameplay) &&
            (MatchStateController.Active == null || MatchStateController.Active.CanAcceptGameplay);

        private void CancelAttack()
        {
            attackTarget = null;
            basicAttack.ClearTarget();
            mover.Stop();
        }

        private void ClearActiveOrderAndStopMovement()
        {
            attackTarget = null;
            basicAttack.ClearTarget();
            mover.Stop();
        }

        private void OnDied(Health _)
        {
            ClearActiveOrderAndStopMovement();
        }
    }
}
