using System;

namespace CierzoArena.Combat
{
    /// <summary>Small transport-agnostic event boundary for confirmed combat results.</summary>
    public static class CombatEvents
    {
        public static event Action<Health, DamageContext> DamageApplied;

        public static void RaiseDamageApplied(Health victim, DamageContext context)
        {
            DamageApplied?.Invoke(victim, context);
        }
    }
}
