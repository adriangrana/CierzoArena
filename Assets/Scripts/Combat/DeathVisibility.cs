using UnityEngine;

namespace CierzoArena.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class DeathVisibility : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderersToDisable;
        [SerializeField] private Collider[] collidersToDisable;

        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void OnDied(Health _)
        {
            SetVisible(false);
        }

        /// <summary>Applies the presentation and collision state for death and respawn.</summary>
        public void SetVisible(bool visible)
        {
            if (renderersToDisable != null)
            {
                foreach (Renderer targetRenderer in renderersToDisable)
                {
                    if (targetRenderer != null)
                    {
                        targetRenderer.enabled = visible;
                    }
                }
            }

            if (collidersToDisable != null)
            {
                foreach (Collider targetCollider in collidersToDisable)
                {
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = visible;
                    }
                }
            }
        }
    }
}
