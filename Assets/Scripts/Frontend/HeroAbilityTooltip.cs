using UnityEngine;
using UnityEngine.EventSystems;

namespace CierzoArena.Frontend
{
    /// <summary>Shows the matching detail tooltip without coupling UI to ability logic.</summary>
    public sealed class HeroAbilityTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private GameObject tooltip;

        public void Configure(GameObject value)
        {
            tooltip = value;
            if (tooltip != null) tooltip.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData _)
        {
            if (tooltip != null) tooltip.SetActive(true);
        }

        public void OnPointerExit(PointerEventData _)
        {
            if (tooltip != null) tooltip.SetActive(false);
        }
    }
}
