using System;
using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Decoupled holder for the client's local hero (Milestone 4.3). It lets the MOBA
    /// camera follow "the unit this client controls" without ever knowing about Netcode
    /// for GameObjects: whoever owns a unit (a local scene, a host or a client) pushes
    /// that unit's <see cref="Transform"/> in here, and the camera reads it. It lives in
    /// the Runtime assembly, so both plain scenes and the Netcode layer can register
    /// against it while Runtime stays Netcode-agnostic.
    ///
    /// It is an explicit scene component (no persistence across scenes yet, no
    /// <c>DontDestroyOnLoad</c>): create one per scene, assign it to the controller and
    /// to whatever registers the hero. It never searches the scene for objects.
    ///
    /// The static <see cref="Active"/> is a deliberately small, controlled access point
    /// for one narrow need: network-spawned unit prefabs cannot hold a serialized scene
    /// reference, so they resolve the scene's provider once at spawn time. It mirrors the
    /// currently enabled provider (set in <see cref="OnEnable"/>, cleared in
    /// <see cref="OnDisable"/>), so it is trivial to reset in tests and is not a general
    /// service locator.
    /// </summary>
    public sealed class LocalHeroProvider : MonoBehaviour
    {
        /// <summary>The current local hero, or a Unity-null reference when none.</summary>
        public Transform CurrentHero { get; private set; }

        /// <summary>Raised whenever the local hero changes (including to null).</summary>
        public event Action<Transform> HeroChanged;

        /// <summary>
        /// The enabled scene provider, for spawn-time resolution by network prefabs that
        /// cannot serialize a scene reference. First enabled provider wins; cleared on
        /// disable so tests start clean.
        /// </summary>
        public static LocalHeroProvider Active { get; private set; }

        private void OnEnable()
        {
            if (Active == null)
            {
                Active = this;
            }
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        /// <summary>
        /// Registers <paramref name="hero"/> as the local hero. A null (or destroyed)
        /// transform is ignored. Registering the same hero again is a no-op and emits no
        /// event; registering a different hero cleanly replaces the previous one and
        /// notifies once.
        /// </summary>
        public void Register(Transform hero)
        {
            if (hero == null)
            {
                return;
            }

            if (CurrentHero == hero)
            {
                return;
            }

            CurrentHero = hero;
            HeroChanged?.Invoke(CurrentHero);
        }

        /// <summary>
        /// Clears the local hero, but only if <paramref name="hero"/> is the one
        /// currently registered. Unregistering a different (or null) transform leaves the
        /// current hero untouched. When it does clear, it notifies once with null.
        /// </summary>
        public void Unregister(Transform hero)
        {
            if (hero == null)
            {
                return;
            }

            if (CurrentHero != hero)
            {
                return;
            }

            CurrentHero = null;
            HeroChanged?.Invoke(null);
        }
    }
}
