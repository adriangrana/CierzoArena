using CierzoArena.Core;
using CierzoArena.Frontend;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class HeroRosterTests
    {
        [Test]
        public void DevelopmentCatalogContainsSixStableHeroesWithCompleteKits()
        {
            HeroCatalog catalog=HeroCatalog.Shared;
            Assert.IsTrue(catalog.Validate(out string reason),reason);
            Assert.AreEqual(6,catalog.Heroes.Count);
            foreach(HeroDefinition hero in catalog.Heroes)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(hero.HeroId));
                for(int slot=0;slot<4;slot++)Assert.IsNotNull(hero.GetAbility(slot),$"{hero.HeroId} slot {slot}");
            }
        }

        [Test]
        public void UnknownSelectionUsesStableFallbackAndTransitionRetainsHeroId()
        {
            HeroCatalog catalog=HeroCatalog.Shared;HeroDefinition fallback=catalog.DefaultHero;
            Assert.AreSame(fallback,catalog.ResolveOrFallback("missing.hero"));
            FrontendLaunchRequest.Set(FrontendMatchMode.LocalDevelopment,TeamId.Ember,"127.0.0.1",7777,"skyline_marksman");
            Assert.IsTrue(FrontendLaunchRequest.TryConsume(out _,out TeamId team,out _,out _,out string heroId));
            Assert.AreEqual(TeamId.Ember,team);Assert.AreEqual("skyline_marksman",heroId);
        }

        [Test]
        public void EmptyAndDuplicateHeroIdsAreRejectedByCatalogValidation()
        {
            HeroCatalog catalog=ScriptableObject.CreateInstance<HeroCatalog>();
            HeroDefinition[] roster=new HeroDefinition[6];
            for(int i=0;i<roster.Length;i++){roster[i]=ScriptableObject.CreateInstance<HeroDefinition>();roster[i].ConfigureRuntime(i==5?"":"duplicate","Name","Title","Description",HeroRole.Vanguard,HeroRole.Support,HeroAttackStyle.Melee,1,Color.white,new HeroStats(1,0,1,0,0,0,1,0,1,0,1,1,0,0,1),new AbilityDefinition[4]);}
            catalog.SetDefinitionsForTests(roster);
            Assert.IsFalse(catalog.Validate(out _));
            Object.DestroyImmediate(catalog);foreach(HeroDefinition hero in roster)Object.DestroyImmediate(hero);
        }

        [Test]
        public void SameHeroDefinitionKeepsIdentityAcrossTeams()
        {
            GameObject azure=new GameObject("Azure Hero");GameObject ember=new GameObject("Ember Hero");
            try
            {
                HeroMatchIdentity azureIdentity=azure.AddComponent<HeroMatchIdentity>();azure.AddComponent<HeroUnit>();azure.AddComponent<TeamMember>();
                HeroMatchIdentity emberIdentity=ember.AddComponent<HeroMatchIdentity>();ember.AddComponent<HeroUnit>();ember.AddComponent<TeamMember>();
                HeroDefinition hero=HeroCatalog.Shared.ResolveOrFallback("storm_warden");azureIdentity.ConfigureHero(hero);emberIdentity.ConfigureHero(hero);
                azure.GetComponent<TeamMember>().ConfigureTeam(TeamId.Azure);ember.GetComponent<TeamMember>().ConfigureTeam(TeamId.Ember);
                Assert.AreEqual(hero.HeroId,azureIdentity.HeroDefinitionId);Assert.AreEqual(hero.HeroId,emberIdentity.HeroDefinitionId);
                Assert.AreNotEqual(azureIdentity.Team,emberIdentity.Team);
            }
            finally { Object.DestroyImmediate(azure);Object.DestroyImmediate(ember); }
        }

        [Test]
        public void EveryRosterHeroHasAnImportedPortraitAsset()
        {
            foreach(HeroDefinition hero in HeroCatalog.Shared.Heroes)
            {
                Assert.IsNotNull(hero.Portrait,$"{hero.HeroId} is missing its portrait asset.");
                Assert.Greater(hero.Portrait.width,128,$"{hero.HeroId} portrait is too small for the detail panel.");
                Assert.Greater(hero.Portrait.height,128,$"{hero.HeroId} portrait is too small for the detail panel.");
            }
        }
    }
}
