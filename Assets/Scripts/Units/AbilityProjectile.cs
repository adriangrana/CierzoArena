using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    public sealed class AbilityProjectile : MonoBehaviour
    {
        private Health target;
        private TeamMember attacker;
        private float damage;
        private float speed;
        private float remaining;

        public void Configure(Health nextTarget, TeamMember nextAttacker, float nextDamage, float nextSpeed, float lifetime)
        {
            target = nextTarget; attacker = nextAttacker; damage = Mathf.Max(0f, nextDamage);
            speed = Mathf.Max(0.01f, nextSpeed); remaining = Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            if (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying || target == null || !target.IsAlive)
            {
                Dispose(); return;
            }
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { Dispose(); return; }
            Vector3 offset = target.transform.position - transform.position;
            float step = speed * Time.deltaTime;
            if (offset.sqrMagnitude <= 0.09f || step >= offset.magnitude)
            {
                target.ApplyDamage(new DamageContext(attacker, damage, AttackDelivery.Ranged));
                Dispose(); return;
            }
            transform.position += offset.normalized * step;
        }
        private void Dispose()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
