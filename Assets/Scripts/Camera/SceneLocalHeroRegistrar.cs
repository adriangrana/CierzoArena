using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Scene-authored glue that registers a specific hero into a
    /// <see cref="LocalHeroProvider"/> at runtime (Milestone 4.4). It exists so a
    /// non-networked scene (such as the local greybox) can declare "this unit is the
    /// local hero" through the builder, using only the provider's existing public
    /// <see cref="LocalHeroProvider.Register"/> / <see cref="LocalHeroProvider.Unregister"/>
    /// API — no global searches, no Netcode, no persistence across scenes.
    ///
    /// In networked scenes this component is not used: there the owning
    /// <c>NetworkUnitController</c> registers its unit on spawn instead.
    /// </summary>
    public sealed class SceneLocalHeroRegistrar : MonoBehaviour
    {
        [Tooltip("Provider the hero is registered into.")]
        [SerializeField] private LocalHeroProvider provider;
        [Tooltip("Unit registered as the local hero when this component starts.")]
        [SerializeField] private Transform hero;

        private void Start()
        {
            Register();
        }

        private void OnDestroy()
        {
            if (provider != null && hero != null)
            {
                provider.Unregister(hero);
            }
        }

        /// <summary>
        /// Registers the configured hero into the configured provider. No-ops when
        /// either reference is missing, so a half-wired scene never throws. Public so
        /// the wiring can be exercised deterministically in tests without entering play.
        /// </summary>
        public void Register()
        {
            if (provider == null || hero == null)
            {
                return;
            }

            provider.Register(hero);
        }
    }
}
