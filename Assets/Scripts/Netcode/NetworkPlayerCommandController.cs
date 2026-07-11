using CierzoArena.Combat;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    /// <summary>
    /// Client-side intent for the M2.5 spike. It never mutates gameplay state: it
    /// only translates local input into <b>requests</b> sent to the server through
    /// the unit this client owns. The server is the sole authority over acceptance,
    /// movement, combat and death.
    /// </summary>
    public sealed class NetworkPlayerCommandController : MonoBehaviour
    {
        [SerializeField] private Camera commandCamera;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private LayerMask selectableMask;
        [SerializeField] private LayerMask attackableMask;
        [SerializeField] private KeyCode stopKey = KeyCode.S;

        private NetworkUnitController ownedUnit;

        private void Awake()
        {
            if (commandCamera == null)
            {
                commandCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                return;
            }

            if (commandCamera == null)
            {
                commandCamera = Camera.main;
            }

            NetworkUnitController unit = ResolveOwnedUnit();
            if (unit == null || commandCamera == null)
            {
                return;
            }

            if (Input.GetKeyDown(stopKey))
            {
                unit.RequestStopRpc();
            }

            if (Input.GetMouseButtonDown(1))
            {
                IssueCommand(unit);
            }
        }

        private void IssueCommand(NetworkUnitController unit)
        {
            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);

            // Intent resolution only. Whether the target is a valid enemy, alive and
            // in range is decided by the server's domain boundary, not here.
            LayerMask targetMask = attackableMask.value != 0 ? attackableMask : selectableMask;
            if (Physics.Raycast(ray, out RaycastHit unitHit, 500f, targetMask))
            {
                NetworkObject targetObject = unitHit.collider.GetComponentInParent<NetworkObject>();
                if (targetObject != null && targetObject.TryGetComponent(out Health _))
                {
                    unit.RequestAttackRpc(new NetworkObjectReference(targetObject));
                    return;
                }
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask))
            {
                unit.RequestMoveRpc(groundHit.point);
            }
        }

        private NetworkUnitController ResolveOwnedUnit()
        {
            if (ownedUnit != null && ownedUnit.IsSpawned && ownedUnit.IsOwner)
            {
                return ownedUnit;
            }

            ownedUnit = null;
            foreach (NetworkUnitController candidate in FindObjectsByType<NetworkUnitController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (candidate.IsSpawned && candidate.IsOwner)
                {
                    ownedUnit = candidate;
                    break;
                }
            }

            return ownedUnit;
        }
    }
}
