using CierzoArena.Combat;
using UnityEngine;

namespace CierzoArena.Units
{
    public sealed class PlayerCommandController : MonoBehaviour
    {
        [SerializeField] private Camera commandCamera;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private LayerMask selectableMask;

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
                Select(unit);
            }
        }

        private void IssueCommand()
        {
            Ray ray = commandCamera.ScreenPointToRay(Input.mousePosition);
            ClickMover mover = selectedUnit.GetComponent<ClickMover>();

            if (Physics.Raycast(ray, out RaycastHit selectableHit, 500f, selectableMask))
            {
                Health target = selectableHit.collider.GetComponentInParent<Health>();
                if (target != null)
                {
                    mover.AttackMove(target);
                    return;
                }
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, groundMask))
            {
                mover.MoveTo(groundHit.point);
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
    }
}
