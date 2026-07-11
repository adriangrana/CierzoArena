using CierzoArena.Core;

namespace CierzoArena.Combat
{
    /// <summary>Immutable description of damage that was actually accepted by Health.</summary>
    public readonly struct DamageContext
    {
        public DamageContext(TeamMember attacker, float amount, AttackDelivery delivery)
        {
            Attacker = attacker;
            Amount = amount;
            Delivery = delivery;
        }

        public TeamMember Attacker { get; }
        public float Amount { get; }
        public AttackDelivery Delivery { get; }
    }
}
