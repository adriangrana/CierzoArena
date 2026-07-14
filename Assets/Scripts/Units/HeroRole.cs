namespace CierzoArena.Units
{
    /// <summary>Gameplay roles are hero metadata, never team or lane assignments.</summary>
    public enum HeroRole { Vanguard, Carry, Duelist, Mage, Support, Controller, Assassin, Utility }
    public enum HeroAttackStyle { Melee, Ranged }
    public enum HeroDamageType { Physical, Magical, Hybrid }
    public enum HeroPowerCurve { Early, Mid, Late }
}
