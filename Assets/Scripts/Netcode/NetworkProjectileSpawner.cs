using CierzoArena.Combat;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Scene-local server service that spawns replicated projectile visuals.</summary>
    public sealed class NetworkProjectileSpawner : MonoBehaviour
    {
        private static NetworkProjectileSpawner active;
        [SerializeField] private NetworkProjectileVisual projectilePrefab;

        public static NetworkProjectileSpawner Active => active;

        private void Awake()
        {
            active = this;
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        public void SpawnVisual(BasicAttack attacker, Health target)
        {
            if (projectilePrefab == null || attacker == null || target == null ||
                !target.TryGetComponent(out NetworkObject targetObject))
            {
                return;
            }

            NetworkProjectileVisual visual = Instantiate(projectilePrefab, attacker.transform.position + Vector3.up * 1.1f, Quaternion.identity);
            visual.NetworkObject.Spawn();
            visual.Configure(targetObject, attacker.ProjectileSpeed, attacker.ProjectileLifetime);
        }
    }
}
