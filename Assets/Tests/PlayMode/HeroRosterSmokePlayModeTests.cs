using System.Collections;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class HeroRosterSmokePlayModeTests
    {
        [UnityTest]
        public IEnumerator EachRosterHeroExposesFourPlayableAbilityDefinitions()
        {
            foreach(HeroDefinition hero in HeroCatalog.Shared.Heroes)
            {
                Assert.IsNotNull(hero);for(int i=0;i<4;i++)Assert.IsNotNull(hero.GetAbility(i));
                yield return null;
            }
        }
    }
}
