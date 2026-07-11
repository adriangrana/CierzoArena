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

        private SelectableUnit selectedUnit;

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

            if (Input.GetMouseButtonDown(0))
            {
                TrySelect();
            }

            if (Input.GetMouseButtonDown(1) && selectedUnit != null)
            {
                IssueCommand();
            }
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
