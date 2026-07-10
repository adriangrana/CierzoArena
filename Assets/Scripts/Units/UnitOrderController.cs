using CierzoArena.Combat;
using CierzoArena.Core;
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

            if (basicAttack.IsInRange(attackTarget))
            {
                mover.Stop();
                basicAttack.TryAttack(attackTarget);
                return;
            }

            mover.MoveTo(attackTarget.transform.position);
        }

        public bool IssueMove(Vector3 destination)
        {
            if (!CanReceiveOrders)
            {
                return false;
            }

            attackTarget = null;
            mover.MoveTo(destination);
            return true;
        }

        public bool IssueAttack(Health target)
        {
            if (!CanReceiveOrders || !basicAttack.CanAttack(target))
            {
                return false;
            }

            attackTarget = target;
            return true;
        }

        public void Stop()
        {
            attackTarget = null;
            mover.Stop();
        }

        private bool CanReceiveOrders => health != null && health.IsAlive;

        private void CancelAttack()
        {
            attackTarget = null;
            mover.Stop();
        }

        private void OnDied(Health _)
        {
            Stop();
        }
    }
}
