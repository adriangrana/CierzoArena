using CierzoArena.Combat;
using CierzoArena.CameraSystem;
using CierzoArena.Units;
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
        [SerializeField] private KeyCode abilityOneKey = KeyCode.Q;
        [SerializeField] private KeyCode abilityTwoKey = KeyCode.W;
        [SerializeField] private KeyCode abilityThreeKey = KeyCode.E;
        [SerializeField] private KeyCode ultimateKey = KeyCode.R;

        private NetworkUnitController ownedUnit;
        private int pendingAbilitySlot = -1;

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
            if (Input.GetKeyDown(abilityOneKey)) BeginAbility(unit, 0);
            if (Input.GetKeyDown(abilityTwoKey)) BeginAbility(unit, 1);
            if (Input.GetKeyDown(abilityThreeKey)) BeginAbility(unit, 2);
            if (Input.GetKeyDown(ultimateKey)) BeginAbility(unit, 3);
            if (Input.GetKeyDown(KeyCode.Escape)) pendingAbilitySlot = -1;

            if (Input.GetMouseButtonDown(0) && ConfirmAbility(unit)) return;

            if (Input.GetMouseButtonDown(1))
            {
                if (pendingAbilitySlot >= 0) { pendingAbilitySlot = -1; return; }
                IssueCommand(unit);
            }
        }

        private void BeginAbility(NetworkUnitController unit, int slot)
        {
            HeroAbilities abilities = unit.GetComponent<HeroAbilities>(); NetworkHeroAbilities networkAbilities = unit.GetComponent<NetworkHeroAbilities>();
            AbilityDefinition definition = abilities != null ? abilities.GetDefinition(slot) : null;
            if (definition == null || networkAbilities == null) return;
            if (definition.Targeting == AbilityTargeting.NoTarget) networkAbilities.RequestCast(slot, null, unit.transform.position);
            else pendingAbilitySlot = slot;
        }

        private bool ConfirmAbility(NetworkUnitController unit)
        {
            if (pendingAbilitySlot < 0) return false;
            HeroAbilities abilities = unit.GetComponent<HeroAbilities>(); NetworkHeroAbilities networkAbilities = unit.GetComponent<NetworkHeroAbilities>();
            AbilityDefinition definition = abilities != null ? abilities.GetDefinition(pendingAbilitySlot) : null;
            if (definition == null || networkAbilities == null) { pendingAbilitySlot = -1; return true; }
            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);
            if (definition.Targeting == AbilityTargeting.UnitTarget && Physics.Raycast(ray, out RaycastHit unitHit, 500f, attackableMask))
            {
                Health target = unitHit.collider.GetComponentInParent<Health>(); networkAbilities.RequestCast(pendingAbilitySlot, target, target != null ? target.transform.position : Vector3.zero); pendingAbilitySlot = -1; return true;
            }
            if (definition.Targeting == AbilityTargeting.PointTarget && Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask))
            {
                networkAbilities.RequestCast(pendingAbilitySlot, null, groundHit.point); pendingAbilitySlot = -1; return true;
            }
            return true;
        }

        private void IssueCommand(NetworkUnitController unit)
        {
            if(MinimapFeedback.TryGetWorldPositionAtScreenPoint(Input.mousePosition,out Vector3 minimapDestination))
            {
                unit.RequestMoveRpc(minimapDestination);
                return;
            }

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
            foreach (NetworkUnitController candidate in FindObjectsByType<NetworkUnitController>(FindObjectsInactive.Exclude))
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
