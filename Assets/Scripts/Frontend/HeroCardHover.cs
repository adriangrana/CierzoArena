using UnityEngine;
using UnityEngine.EventSystems;

namespace CierzoArena.Frontend
{
    /// <summary>Small presentation-only hover lift used by hero cards.</summary>
    public sealed class HeroCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private float hoverScale = 1.025f;
        private bool hovering;

        public void Configure(float value) => hoverScale = Mathf.Max(1f, value);
        public void OnPointerEnter(PointerEventData _) => hovering = true;
        public void OnPointerExit(PointerEventData _) => hovering = false;

        private void Update()
        {
            float target = hovering ? hoverScale : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, Time.unscaledDeltaTime * 12f);
        }
    }
}
