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
            GameObject providerObject=new GameObject("Provider");LocalHeroProvider provider=providerObject.AddComponent<LocalHeroProvider>();
            GameObject hudObject=new GameObject("GameplayHUD");CompetitiveGameplayHud hud=hudObject.AddComponent<CompetitiveGameplayHud>();
            GameObject hero=new GameObject("Local Hero");provider.Register(hero.transform);
            hud.BindHero(hero.transform);
            Assert.That(hud.BoundHero,Is.EqualTo(hero.transform));Assert.That(hud.IsBoundToLocalHero,Is.True);
            Assert.That(hud.AbilitySlotCount,Is.EqualTo(4));Assert.That(hud.InventorySlotCount,Is.EqualTo(6));
            Object.DestroyImmediate(hero);Object.DestroyImmediate(hudObject);Object.DestroyImmediate(providerObject);
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
