using CierzoArena.Combat;
using CierzoArena.CameraSystem;
using CierzoArena.Core;
using UnityEngine;

namespace CierzoArena.Units
{
    public sealed class PlayerCommandController : MonoBehaviour
    {
        [SerializeField] private Camera commandCamera;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private LayerMask selectableMask;
        [SerializeField] private LayerMask attackableMask;
        [SerializeField] private KeyCode attackMoveKey = KeyCode.A;
        [SerializeField] private KeyCode stopKey = KeyCode.S;
        [SerializeField] private KeyCode abilityOneKey = KeyCode.Q;
        [SerializeField] private KeyCode abilityTwoKey = KeyCode.W;
        [SerializeField] private KeyCode abilityThreeKey = KeyCode.E;
        [SerializeField] private KeyCode ultimateKey = KeyCode.R;

        private SelectableUnit selectedUnit;
        private int pendingAbilitySlot = -1;
        private bool pendingAttackMove;

        private void Awake()
        {
            if (commandCamera == null)
            {
                commandCamera = Camera.main;
            }
        }

        private void Start()
        {
            // The local MOBA scene starts with its controllable hero ready for an
            // immediate right-click command; the visible ring and command receiver
            // must agree on that initial selection.
            if (selectedUnit == null)
            {
                Select(FindDefaultHero());
            }
        }

        private void Update()
        {
            // The active-match menu is a real input boundary, not a visual mask.
            // Clear pending targeting so closing it cannot release an old command on
            // the first click back in the arena.
            if (!MatchNavigationState.IsGameplayInputAllowed)
            {
                pendingAbilitySlot = -1;
                pendingAttackMove = false;
                return;
            }
            if (selectedUnit == null && LocalHeroProvider.Active != null && LocalHeroProvider.Active.CurrentHero != null)
            {
                Select(LocalHeroProvider.Active.CurrentHero.GetComponent<SelectableUnit>());
            }
            if (selectedUnit != null && !IsLocalHero(selectedUnit))
            {
                Select(null);
            }

            if (selectedUnit != null && Input.GetKeyDown(stopKey) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            {
                UnitOrderController selectedOrders = selectedUnit.GetComponent<UnitOrderController>();
                if (selectedOrders != null)
                {
                    selectedOrders.Execute(UnitOrderCommand.Stop());
                }
            }

            if (commandCamera == null)
            {
                return;
            }

            bool inventoryModifier = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (!inventoryModifier && Input.GetKeyDown(abilityOneKey)) BeginAbility(0);
            if (!inventoryModifier && Input.GetKeyDown(abilityTwoKey)) BeginAbility(1);
            if (!inventoryModifier && Input.GetKeyDown(abilityThreeKey)) BeginAbility(2);
            if (Input.GetKeyDown(ultimateKey)) BeginAbility(3);
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelAbility(); pendingAttackMove=false; }
            if (Input.GetKeyDown(attackMoveKey) && selectedUnit != null && !inventoryModifier)
            {
                CancelAbility();
                pendingAttackMove = true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (ConfirmAbility()) return;
                if (pendingAttackMove)
                {
                    pendingAttackMove = false;
                    IssueAttackMove();
                    return;
                }
                TrySelect();
            }

            if (Input.GetMouseButtonDown(1) && selectedUnit != null)
            {
                if (pendingAbilitySlot >= 0 || pendingAttackMove) { CancelAbility(); pendingAttackMove=false; return; }
                IssueCommand();
            }
        }

        private void BeginAbility(int slot)
        {
            HeroAbilities abilities = selectedUnit != null ? selectedUnit.GetComponent<HeroAbilities>() : null;
            AbilityDefinition definition = abilities != null ? abilities.GetDefinition(slot) : null;
            if (definition == null) return;
            if (definition.Targeting == AbilityTargeting.NoTarget)
            {
                RequestCast(abilities, slot, null, selectedUnit.transform.position);
                return;
            }
            pendingAbilitySlot = slot;
        }

        private bool ConfirmAbility()
        {
            if (pendingAbilitySlot < 0 || selectedUnit == null) return false;
            HeroAbilities abilities = selectedUnit.GetComponent<HeroAbilities>(); AbilityDefinition definition = abilities != null ? abilities.GetDefinition(pendingAbilitySlot) : null;
            if (definition == null) { pendingAbilitySlot = -1; return true; }
            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);
            if (definition.Targeting == AbilityTargeting.UnitTarget && Physics.Raycast(ray, out RaycastHit hit, 500f, attackableMask))
            {
                Health target = hit.collider.GetComponentInParent<Health>();
                RequestCast(abilities, pendingAbilitySlot, target, target != null ? target.transform.position : Vector3.zero);
                pendingAbilitySlot = -1; return true;
            }
            if (definition.Targeting == AbilityTargeting.PointTarget && Physics.Raycast(ray, out RaycastHit ground, 500f, groundMask))
            {
                RequestCast(abilities, pendingAbilitySlot, null, ground.point); pendingAbilitySlot = -1; return true;
            }
            return true;
        }

        private void CancelAbility()
        {
            pendingAbilitySlot = -1;
            if (selectedUnit != null) selectedUnit.GetComponent<HeroAbilities>()?.CancelBeforeRelease();
        }

        private void TrySelect()
        {
            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 500f, selectableMask))
            {
                SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
                Select(IsPlayerControlled(unit) ? unit : null);
            }
        }

        private void IssueCommand()
        {
            UnitOrderController orders = selectedUnit.GetComponent<UnitOrderController>();
            if (orders == null)
            {
                return;
            }

            if(MinimapFeedback.TryGetWorldPositionAtScreenPoint(Input.mousePosition,out Vector3 minimapDestination))
            {
                orders.Execute(UnitOrderCommand.Move(minimapDestination));
                return;
            }

            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);

            // Resolve player intent only: clicked unit -> Attack request, ground -> Move
            // request. Whether the order is valid (enemy, alive, in team rules) is
            // decided by the order boundary, not here, to avoid duplicated validation.
            LayerMask targetMask = attackableMask.value != 0 ? attackableMask : selectableMask;
            if (Physics.Raycast(ray, out RaycastHit unitHit, 500f, targetMask))
            {
                Health targetHealth = unitHit.collider.GetComponentInParent<Health>();
                if (targetHealth != null)
                {
                    orders.Execute(UnitOrderCommand.Attack(targetHealth));
                }

                return;
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask))
            {
                orders.Execute(UnitOrderCommand.Move(groundHit.point));
            }
        }

        private void IssueAttackMove()
        {
            if (selectedUnit == null) return;
            UnitOrderController orders = selectedUnit.GetComponent<UnitOrderController>();
            if (orders == null) return;
            if (MinimapFeedback.TryGetWorldPositionAtScreenPoint(Input.mousePosition, out Vector3 minimapDestination))
            {
                orders.Execute(UnitOrderCommand.AttackMove(minimapDestination));
                return;
            }

            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);
            LayerMask targetMask = attackableMask.value != 0 ? attackableMask : selectableMask;
            if (Physics.Raycast(ray, out RaycastHit unitHit, 500f, targetMask))
            {
                Health targetHealth = unitHit.collider.GetComponentInParent<Health>();
                if (targetHealth != null)
                {
                    orders.Execute(UnitOrderCommand.Attack(targetHealth));
                    return;
                }
            }
            if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask))
            {
                orders.Execute(UnitOrderCommand.AttackMove(groundHit.point));
            }
        }

        private void Select(SelectableUnit unit)
        {
            if (selectedUnit != null)
            {
                selectedUnit.SetSelected(false);
            }

            selectedUnit = unit;

            if (selectedUnit != null)
            {
                selectedUnit.SetSelected(true);
            }
        }

        private static bool IsPlayerControlled(SelectableUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            Health health = unit.GetComponent<Health>();
            return health != null && health.IsAlive && IsLocalHero(unit);
        }

        private static bool IsLocalHero(SelectableUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            return unit.TryGetComponent(out HeroUnit _) && LocalHeroProvider.Active != null && LocalHeroProvider.Active.CurrentHero == unit.transform;
        }

        private static SelectableUnit FindDefaultHero()
        {
            foreach (SelectableUnit candidate in FindObjectsByType<SelectableUnit>(FindObjectsInactive.Exclude))
            {
                if (IsLocalHero(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void RequestCast(HeroAbilities abilities, int slot, Health target, Vector3 point)
        {
            if(abilities==null)return;
            foreach(MonoBehaviour component in abilities.GetComponents<MonoBehaviour>())
            {
                if(component is IHeroAbilityRequestGateway gateway && gateway.IsReady){gateway.RequestCast(slot,target,point);return;}
            }
            abilities.TryStartCast(slot,target,point);
        }
    }
}
