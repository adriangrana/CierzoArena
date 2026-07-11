using CierzoArena.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(Collider))]
    public sealed class ShopZone : MonoBehaviour
    {
        [SerializeField] private TeamId team;
        [SerializeField] private ItemCatalog catalog;
        private readonly static HashSet<ShopZone> activeZones = new();
        private Collider zoneCollider;
        public TeamId Team => team;
        public ItemCatalog Catalog => catalog;
        public bool Contains(Vector3 position) => (zoneCollider != null || TryGetComponent(out zoneCollider)) && zoneCollider.bounds.Contains(position);

        private void Awake() => TryGetComponent(out zoneCollider);
        private void OnEnable() => activeZones.Add(this);
        private void OnDisable() => activeZones.Remove(this);

        public static ShopZone FindFriendlyContaining(TeamId requestedTeam, Vector3 position)
        {
            foreach (ShopZone zone in activeZones)
            {
                if (zone != null && zone.Team == requestedTeam && zone.Contains(position))
                {
                    return zone;
                }
            }

            return null;
        }
    }
}
