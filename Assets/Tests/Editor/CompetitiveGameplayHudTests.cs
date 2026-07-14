using CierzoArena.CameraSystem;
using CierzoArena.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class CompetitiveGameplayHudTests
    {
        [Test]
        public void HudHasOneLocalBindingAndExpectedCommandSlots()
        {
            LocalHeroProvider provider=LocalHeroProvider.Active;
            GameObject providerObject=null;
            if(provider==null)
            {
                providerObject=new GameObject("Provider");
                provider=providerObject.AddComponent<LocalHeroProvider>();
            }

            Transform previousHero=provider.CurrentHero;
            GameObject hudObject=new GameObject("GameplayHUD");
            GameObject hero=new GameObject("Local Hero");
            try
            {
                CompetitiveGameplayHud hud=hudObject.AddComponent<CompetitiveGameplayHud>();
                provider.Register(hero.transform);
                Assert.That(hud.BoundHero,Is.EqualTo(hero.transform));Assert.That(hud.IsBoundToLocalHero,Is.True);
                Assert.That(hud.AbilitySlotCount,Is.EqualTo(4));Assert.That(hud.InventorySlotCount,Is.EqualTo(6));
            }
            finally
            {
                if(previousHero!=null)provider.Register(previousHero);else provider.Unregister(hero.transform);
                Object.DestroyImmediate(hero);Object.DestroyImmediate(hudObject);if(providerObject!=null)Object.DestroyImmediate(providerObject);
            }
        }
        [Test]
        public void HudBindingCanBeClearedAndReassignedWithoutGlobalHeroSearch()
        {
            GameObject hudObject=new GameObject("GameplayHUD");CompetitiveGameplayHud hud=hudObject.AddComponent<CompetitiveGameplayHud>();
            GameObject first=new GameObject("First");GameObject second=new GameObject("Second");
            hud.BindHero(first.transform);hud.ClearBinding();hud.BindHero(second.transform);
            Assert.That(hud.BoundHero,Is.EqualTo(second.transform));
            Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(hudObject);
        }
    }
}
