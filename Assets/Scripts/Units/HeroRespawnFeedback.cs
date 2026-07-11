using CierzoArena.CameraSystem;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Minimal local-only respawn counter; visual feedback never drives simulation.</summary>
    [RequireComponent(typeof(HeroLifeCycle))]
    public sealed class HeroRespawnFeedback : MonoBehaviour
    {
        private HeroLifeCycle life;

        private void Awake() => life = GetComponent<HeroLifeCycle>();

        private void OnGUI()
        {
            if (life == null || life.State == HeroLifeState.Alive || LocalHeroProvider.Active == null ||
                LocalHeroProvider.Active.CurrentHero != transform)
            {
                return;
            }

            GUI.Label(new Rect(24f, Screen.height - 72f, 360f, 38f), $"Respawning in {Mathf.CeilToInt(life.RespawnRemaining)}", GUI.skin.box);
        }
    }
}
