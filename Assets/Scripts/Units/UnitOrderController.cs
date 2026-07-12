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
        [SerializeField, Min(1f)] private float attackMoveAcquireRange = 8f;
        [SerializeField, Min(.05f)] private float attackMoveArrivalDistance = .2f;
        private bool attackMoveActive;
        private Vector3 attackMoveDestination;

        public bool IsAttackMoveActive => attackMoveActive;
        public Vector3 AttackMoveDestination => attackMoveDestination;

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
            if (!CanReceiveOrders)
            {
                if (attackTarget != null || attackMoveActive) ClearActiveOrderAndStopMovement();
                return;
            }

            if (attackTarget != null)
            {
                if (!basicAttack.CanAttack(attackTarget))
                {
                    attackTarget = null;
                    basicAttack.ClearTarget();
                    if (!attackMoveActive) mover.Stop();
                }
                else
                {
                    basicAttack.SetTarget(attackTarget);
                    basicAttack.Simulate(Time.deltaTime);
                    if (!basicAttack.NeedsApproach)
                    {
                        mover.Stop();
                    }
                    else
                    {
                        mover.MoveTo(basicAttack.GetApproachPosition(attackTarget));
                    }
                    return;
                }
            }

            if (!attackMoveActive)
            {
                return;
            }

            Health candidate = FindAttackMoveTarget();
            if (candidate != null)
            {
                attackTarget = candidate;
                basicAttack.SetTarget(candidate);
                return;
            }

            if ((transform.position - attackMoveDestination).sqrMagnitude <= attackMoveArrivalDistance * attackMoveArrivalDistance)
            {
                attackMoveActive = false;
                mover.Stop();
                return;
            }

            mover.MoveTo(attackMoveDestination);
        }

        public bool IssueMove(Vector3 destination)
        {
            return Execute(UnitOrderCommand.Move(destination));
        }

        public bool IssueAttack(Health target)
        {
            return Execute(UnitOrderCommand.Attack(target));
        }

        public bool IssueAttackMove(Vector3 destination)
        {
            return Execute(UnitOrderCommand.AttackMove(destination));
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
                    TryGetComponent(out HeroAbilities abilities);
                    abilities?.CancelBeforeRelease();
                    ClearAttackState();
                    mover.MoveTo(command.Destination);
                    return true;
                case UnitOrderType.Attack:
                    TryGetComponent(out HeroAbilities attackAbilities);
                    attackAbilities?.CancelBeforeRelease();
                    attackMoveActive = false;
                    basicAttack.SetTarget(command.Target);
                    attackTarget = command.Target;
                    return true;
                case UnitOrderType.AttackMove:
                    TryGetComponent(out HeroAbilities attackMoveAbilities);
                    attackMoveAbilities?.CancelBeforeRelease();
                    attackTarget = null;
                    basicAttack.ClearTarget();
                    attackMoveDestination = command.Destination;
                    attackMoveActive = true;
                    mover.MoveTo(attackMoveDestination);
                    return true;
                case UnitOrderType.Stop:
                    TryGetComponent(out HeroAbilities stopAbilities);
                    stopAbilities?.CancelBeforeRelease();
                    ClearAttackState();
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
                case UnitOrderType.AttackMove:
                    return CanReceiveOrders && (!TryGetComponent(out StatusEffectController moveEffects) || moveEffects.CanMove);
                case UnitOrderType.Attack:
                    return CanReceiveOrders && (!TryGetComponent(out StatusEffectController attackEffects) || attackEffects.CanAttack) && basicAttack.CanAttack(command.Target);
                case UnitOrderType.Stop:
                    return CanReceiveOrders;
                default:
                    return false;
            }
        }

        private bool CanReceiveOrders => health != null && health.IsAlive &&
            (!TryGetComponent(out HeroLifeCycle heroLife) || heroLife.IsAliveForGameplay) &&
            (MatchStateController.Active == null || MatchStateController.Active.CanAcceptGameplay);

        private Health FindAttackMoveTarget()
        {
            float closestDistance = attackMoveAcquireRange * attackMoveAcquireRange;
            Health closest = null;
            foreach (Health candidate in FindObjectsByType<Health>(FindObjectsInactive.Exclude))
            {
                if (!basicAttack.CanAttack(candidate)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance > closestDistance) continue;
                closestDistance = distance;
                closest = candidate;
            }
            return closest;
        }

        private void ClearAttackState()
        {
            attackMoveActive = false;
            attackTarget = null;
            basicAttack.ClearTarget();
        }

        private void ClearActiveOrderAndStopMovement()
        {
            ClearAttackState();
            mover.Stop();
        }

        private void OnDied(Health _)
        {
            ClearActiveOrderAndStopMovement();
        }
    }
}
