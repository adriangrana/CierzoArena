using CierzoArena.Combat;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>Networked presentation twin of a Runtime-authoritative projectile.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkProjectileVisual : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> targetReference = new();
        private readonly NetworkVariable<float> speed = new(14f);
        private readonly NetworkVariable<float> lifetime = new(4f);
        private float elapsed;

        public void Configure(NetworkObject target, float visualSpeed, float visualLifetime)
        {
            if (!IsServer || target == null)
            {
                return;
            }

            targetReference.Value = new NetworkObjectReference(target);
            speed.Value = Mathf.Max(0.01f, visualSpeed);
            lifetime.Value = Mathf.Max(0.01f, visualLifetime);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= lifetime.Value || !targetReference.Value.TryGet(out NetworkObject targetObject) ||
                !targetObject.TryGetComponent(out Health targetHealth) || !targetHealth.IsAlive)
            {
                NetworkObject.Despawn(true);
                return;
            }

            Vector3 offset = targetObject.transform.position - transform.position;
            float step = speed.Value * Time.deltaTime;
            if (offset.sqrMagnitude <= 0.04f || step >= offset.magnitude)
            {
                NetworkObject.Despawn(true);
                return;
            }

            transform.position += offset.normalized * step;
            transform.rotation = Quaternion.LookRotation(offset);
        }
    }
}
