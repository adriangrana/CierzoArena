using UnityEngine;

namespace CierzoArena.Units
{
    public sealed class SelectableUnit : MonoBehaviour
    {
        [SerializeField] private Renderer selectionRing;

        public bool IsSelected { get; private set; }

        private void Awake()
        {
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selectionRing != null)
            {
                selectionRing.enabled = selected;
            }
        }
    }
}
