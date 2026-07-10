using CierzoArena.Combat;
using UnityEngine;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(BasicAttack))]
    public sealed class ClickMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float stoppingDistance = 0.15f;
        [SerializeField] private float turnSpeed = 12f;

        private BasicAttack basicAttack;
        private Vector3 destination;
        private Health attackTarget;
        private bool hasDestination;

        private void Awake()
        {
            basicAttack = GetComponent<BasicAttack>();
            destination = transform.position;
        }

        private void Update()
        {
            if (attackTarget != null && attackTarget.IsAlive)
            {
                destination = attackTarget.transform.position;
                hasDestination = true;

                if (basicAttack.TryAttack(attackTarget))
                {
                    Face(destination);
                }
            }

            if (!hasDestination)
            {
                return;
            }

            Vector3 flatTarget = new Vector3(destination.x, transform.position.y, destination.z);
            Vector3 delta = flatTarget - transform.position;
            float distance = delta.magnitude;
            float desiredStop = attackTarget != null ? basicAttack.Range * 0.9f : stoppingDistance;

            if (distance <= desiredStop)
            {
                hasDestination = attackTarget != null;
                return;
            }

            Vector3 direction = delta.normalized;
            transform.position += direction * (moveSpeed * Time.deltaTime);
            Face(transform.position + direction);
        }

        public void MoveTo(Vector3 worldPosition)
        {
            attackTarget = null;
            destination = worldPosition;
            hasDestination = true;
        }

        public void AttackMove(Health target)
        {
            if (target == null || !basicAttack.CanAttack(target))
            {
                return;
            }

            attackTarget = target;
            destination = target.transform.position;
            hasDestination = true;
        }

        private void Face(Vector3 worldPosition)
        {
            Vector3 look = worldPosition - transform.position;
            look.y = 0f;

            if (look.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }
}
