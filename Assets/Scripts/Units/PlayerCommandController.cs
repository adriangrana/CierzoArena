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
        [SerializeField] private KeyCode stopKey = KeyCode.S;

        private SelectableUnit selectedUnit;

        private void Awake()
        {
            if (commandCamera == null)
            {
                commandCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (selectedUnit != null && !IsPlayerControlled(selectedUnit))
            {
                Select(null);
            }

            if (selectedUnit != null && Input.GetKeyDown(stopKey))
            {
                UnitOrderController selectedOrders = selectedUnit.GetComponent<UnitOrderController>();
                if (selectedOrders != null)
                {
                    selectedOrders.Stop();
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

            if (Physics.Raycast(ray, out RaycastHit unitHit, 500f, selectableMask))
            {
                Health targetHealth = unitHit.collider.GetComponentInParent<Health>();
                TeamMember selectedTeam = selectedUnit.GetComponent<TeamMember>();
                TeamMember targetTeam = targetHealth != null ? targetHealth.GetComponent<TeamMember>() : null;

                if (selectedTeam != null && selectedTeam.IsEnemy(targetTeam))
                {
                    orders.IssueAttack(targetHealth);
                }

                return;
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask))
            {
                orders.IssueMove(groundHit.point);
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
    }
}
