namespace CierzoArena.Core
{
    /// <summary>Optional bridge for transports that must finish assigning a hero's
    /// authoritative identity before runtime match statistics register it.</summary>
    public interface IHeroMatchRegistrationGate
    {
        bool IsHeroMatchRegistrationReady { get; }
    }
}
