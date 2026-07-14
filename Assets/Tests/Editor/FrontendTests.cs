using CierzoArena.Core;
using CierzoArena.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class FrontendTests
    {
        [Test] public void NavigationAlwaysResolvesToOneValidPanel(){Assert.That(MainMenuController.ResolveSingleActivePanel(-1,4),Is.EqualTo(0));Assert.That(MainMenuController.ResolveSingleActivePanel(2,4),Is.EqualTo(2));Assert.That(MainMenuController.ResolveSingleActivePanel(9,4),Is.EqualTo(3));}
        [Test] public void FrontendLaunchRequestPreservesOneShotConfiguration(){FrontendLaunchRequest.Set(FrontendMatchMode.Client,TeamId.Ember,"192.168.1.4",9000);Assert.That(FrontendLaunchRequest.TryConsume(out FrontendMatchMode mode,out TeamId team,out string address,out ushort port),Is.True);Assert.That(mode,Is.EqualTo(FrontendMatchMode.Client));Assert.That(team,Is.EqualTo(TeamId.Ember));Assert.That(address,Is.EqualTo("192.168.1.4"));Assert.That(port,Is.EqualTo(9000));Assert.That(FrontendLaunchRequest.TryConsume(out _,out _,out _,out _),Is.False);}
        [Test] public void ThemeDefaultsProvideVisibleCoreColors(){CierzoVisualTheme theme=ScriptableObject.CreateInstance<CierzoVisualTheme>();Assert.That(theme.IsValid(),Is.True);Object.DestroyImmediate(theme);}
    }
}
