using CierzoArena.Combat;
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
        [SerializeField] private KeyCode stopKey = KeyCode.S;
        [SerializeField] private KeyCode abilityOneKey = KeyCode.Q;
        [SerializeField] private KeyCode abilityTwoKey = KeyCode.W;
        [SerializeField] private KeyCode abilityThreeKey = KeyCode.E;
        [SerializeField] private KeyCode ultimateKey = KeyCode.R;

        private SelectableUnit selectedUnit;
        private int pendingAbilitySlot = -1;

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
            if (selectedUnit != null && !IsLocalHero(selectedUnit))
            {
                Select(null);
            }

            if (selectedUnit != null && Input.GetKeyDown(stopKey))
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

            if (Input.GetKeyDown(abilityOneKey)) BeginAbility(0);
            if (Input.GetKeyDown(abilityTwoKey)) BeginAbility(1);
            if (Input.GetKeyDown(abilityThreeKey)) BeginAbility(2);
            if (Input.GetKeyDown(ultimateKey)) BeginAbility(3);
            if (Input.GetKeyDown(KeyCode.Escape)) CancelAbility();

            if (Input.GetMouseButtonDown(0))
            {
                if (!ConfirmAbility()) TrySelect();
            }

            if (Input.GetMouseButtonDown(1) && selectedUnit != null)
            {
                if (pendingAbilitySlot >= 0) { CancelAbility(); return; }
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
                abilities.TryStartCast(slot, null, selectedUnit.transform.position);
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
                abilities.TryStartCast(pendingAbilitySlot, target, target != null ? target.transform.position : Vector3.zero);
                pendingAbilitySlot = -1; return true;
            }
            if (definition.Targeting == AbilityTargeting.PointTarget && Physics.Raycast(ray, out RaycastHit ground, 500f, groundMask))
            {
                abilities.TryStartCast(pendingAbilitySlot, null, ground.point); pendingAbilitySlot = -1; return true;
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
            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);
            UnitOrderController orders = selectedUnit.GetComponent<UnitOrderController>();
            if (orders == null)
            {
                return;
            }

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

            TeamMember teamMember = unit.GetComponent<TeamMember>();
            Health health = unit.GetComponent<Health>();
            return teamMember != null && health != null && health.IsAlive && teamMember.Team == TeamId.Azure;
        }

        private static bool IsLocalHero(SelectableUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            return unit.TryGetComponent(out HeroUnit _) &&
                unit.TryGetComponent(out TeamMember member) && member.Team == TeamId.Azure;
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
    }
}
