using UnityEngine;

namespace CierzoArena.Core
{
    /// <summary>
    /// Reusable, immutable configuration for a unit. Holds only base values shared by
    /// multiple units of the same archetype. Mutable per-match state (current health,
    /// attack cooldowns, movement destinations) never lives here; it stays in the
    /// runtime components (<c>Health</c>, <c>BasicAttack</c>, <c>ClickMover</c>).
    /// </summary>
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Cierzo Arena/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [SerializeField] private float maxHealth = 500f;
        [SerializeField] private float movementSpeed = 5.5f;
        [SerializeField] private float attackDamage = 45f;
        [SerializeField] private float attackRange = 2.1f;
        [SerializeField] private float attacksPerSecond = 0.7f;

        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float MovementSpeed => Mathf.Max(0f, movementSpeed);
        public float AttackDamage => Mathf.Max(0f, attackDamage);
        public float AttackRange => Mathf.Max(0f, attackRange);
        public float AttacksPerSecond => Mathf.Max(0.01f, attacksPerSecond);

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            movementSpeed = Mathf.Max(0f, movementSpeed);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackRange = Mathf.Max(0f, attackRange);
            attacksPerSecond = Mathf.Max(0.01f, attacksPerSecond);
        }
    }
}
