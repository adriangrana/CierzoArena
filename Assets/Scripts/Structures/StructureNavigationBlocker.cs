using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Structures
{
    /// <summary>Supplies the carved navigation footprint for dynamically spawned
    /// tower prefabs. Scene-authored towers already include an equivalent child.</summary>
    [RequireComponent(typeof(StructureEntity))]
    public sealed class StructureNavigationBlocker : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float radius = .55f;
        [SerializeField, Min(.1f)] private float height = 7f;

        private StructureEntity structure;
        private Collider blockerCollider;
        private NavMeshObstacle obstacle;

        private void Awake()
        {
            structure = GetComponent<StructureEntity>();
            if (structure == null || structure.Kind != StructureKind.Tower) return;
            EnsureBlocker();
            structure.Destroyed += OnDestroyed;
        }

        private void OnDestroy()
        {
            if (structure != null) structure.Destroyed -= OnDestroyed;
        }

        private void EnsureBlocker()
        {
            Transform existing = transform.Find("Navigation Blocker");
            GameObject blocker = existing != null ? existing.gameObject : new GameObject("Navigation Blocker");
            blocker.layer = 6; // Ground: matches RuntimeNavMesh's source mask.
            if (existing == null)
            {
                blocker.transform.SetParent(transform, false);
                blocker.transform.localPosition = new Vector3(0f, height * .5f, 0f);
            }

            blockerCollider = blocker.GetComponent<Collider>();
            if (blockerCollider == null)
            {
                CapsuleCollider capsule = blocker.AddComponent<CapsuleCollider>();
                capsule.radius = radius;
                capsule.height = height;
                blockerCollider = capsule;
            }
            else if (blockerCollider is CapsuleCollider capsule)
            {
                capsule.radius = radius;
                capsule.height = height;
            }

            obstacle = blocker.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = radius;
            obstacle.height = height;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        private void OnDestroyed(StructureEntity _)
        {
            if (blockerCollider != null) blockerCollider.enabled = false;
            if (obstacle != null) obstacle.enabled = false;
        }
    }
}
