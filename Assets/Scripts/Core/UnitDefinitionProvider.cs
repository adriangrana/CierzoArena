using UnityEngine;

namespace CierzoArena.Core
{
    /// <summary>
    /// Single source of the reusable <see cref="UnitDefinition"/> for a unit.
    /// Runtime components (Health, BasicAttack, ClickMover) read their base
    /// configuration from this one provider instead of each holding an
    /// independent reference. The provider only exposes immutable configuration;
    /// mutable per-match state stays in the runtime components.
    /// </summary>
    public sealed class UnitDefinitionProvider : MonoBehaviour
    {
        [SerializeField] private UnitDefinition definition;

        public UnitDefinition Definition => definition;
    }
}
