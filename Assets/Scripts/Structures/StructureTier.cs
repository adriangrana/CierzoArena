namespace CierzoArena.Structures
{
    /// <summary>Defensive order from the enemy approach to the core.
    /// Conceptual order is Outer -> Inner -> Gate -> CoreGuard -> Core, but
    /// CoreGuard is appended last so existing serialized structures (which store
    /// the enum by integer) keep their authored tier after the M23 rework.</summary>
    public enum StructureTier
    {
        Outer,
        Inner,
        Gate,
        Core,
        CoreGuard
    }
}
