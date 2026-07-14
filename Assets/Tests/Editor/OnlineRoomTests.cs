using System.Threading;
using CierzoArena.Core;
using CierzoArena.Frontend;
using CierzoArena.Online;
using CierzoArena.Online.Identity;
using CierzoArena.Online.Room;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class OnlineRoomTests
    {
        private OnlineServicesSettings settings;

        [SetUp]
        public void SetUp() => settings = ScriptableObject.CreateInstance<OnlineServicesSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(settings);

        [Test]
        public void DisplayNameIsNormalizedWithoutExposingPlayerId()
        {
            Assert.IsTrue(PlayerDisplayName.TryNormalize("  Ádrían   42 ", out string value, out _));
            Assert.AreEqual("Ádrían 42", value);
            Assert.IsFalse(PlayerDisplayName.TryNormalize("A\nB", out _, out _));
            Assert.AreEqual("Jugador-1234", PlayerDisplayName.FallbackFor("1234-abcdef"));
        }

        [Test]
        public void JoinCodeIsNormalizedAndRejectsNonAlphanumericValues()
        {
            Assert.AreEqual("AB12CD", MultiplayerSessionCoordinator.NormalizeJoinCode(" ab 12 cd "));
            Assert.IsTrue(MultiplayerSessionCoordinator.IsValidJoinCode("AB12CD"));
            Assert.IsFalse(MultiplayerSessionCoordinator.IsValidJoinCode("AB-12"));
        }

        [Test]
        public void RoomCapsEachTeamAtFiveAndNeverDuplicatesAPlayer()
        {
            MatchRoster roster = new MatchRoster();
            for (int i = 0; i < 5; i++) Assert.IsTrue(roster.TryAdd(Player("azure-" + i, TeamId.Azure), settings, out _));
            Assert.IsFalse(roster.TryAdd(Player("azure-5", TeamId.Azure), settings, out OnlineErrorCode full));
            Assert.AreEqual(OnlineErrorCode.SessionFull, full);
            Assert.IsFalse(roster.TryAdd(Player("azure-0", TeamId.Ember), settings, out OnlineErrorCode duplicate));
            Assert.AreEqual(OnlineErrorCode.AlreadyJoined, duplicate);
            for (int i = 0; i < 5; i++) Assert.AreEqual(i, roster.Players[i].StableSlot);
        }

        [Test]
        public void ChangingTeamsReleasesOldSlotAndResetsReady()
        {
            MatchRoster roster = new MatchRoster();
            MatchPlayerSlot host = Player("host", TeamId.Azure); host.IsReady = true;
            Assert.IsTrue(roster.TryAdd(host, settings, out _));
            Assert.IsTrue(roster.TryChangeTeam("host", TeamId.Ember, settings, out _));
            Assert.AreEqual(TeamId.Ember, host.Team);
            Assert.AreEqual(0, host.StableSlot);
            Assert.IsFalse(host.IsReady);
        }

        [Test]
        public void OnlyHostCanStartAndEveryPlayerMustBeReadyAndCompatible()
        {
            MatchRoster roster = new MatchRoster();
            MatchPlayerSlot host = Player("host", TeamId.Azure); host.IsReady = true;
            MatchPlayerSlot guest = Player("guest", TeamId.Ember); guest.IsReady = false;
            roster.ConfigureHost(host.PlayerId);
            Assert.IsTrue(roster.TryAdd(host, settings, out _));
            Assert.IsTrue(roster.TryAdd(guest, settings, out _));
            Assert.IsFalse(roster.CanStart("guest", settings, out OnlineErrorCode denied));
            Assert.AreEqual(OnlineErrorCode.PermissionDenied, denied);
            Assert.IsFalse(roster.CanStart("host", settings, out _));
            Assert.IsTrue(roster.TrySetReady("guest", "guest", true, out _));
            Assert.IsTrue(roster.CanStart("host", settings, out _));
            guest.ProtocolVersion++;
            Assert.IsFalse(roster.CanStart("host", settings, out OnlineErrorCode incompatible));
            Assert.AreEqual(OnlineErrorCode.VersionMismatch, incompatible);
        }

        [Test]
        public void OfflineIdentityPreservesLocalModeWhenCloudServicesAreUnavailable()
        {
            OfflinePlayerIdentityService offline = new OfflinePlayerIdentityService("m24-test");
            offline.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(PlayerIdentityState.Offline, offline.State);
            Assert.IsFalse(offline.IsOnline);
            Assert.IsFalse(offline.IsAuthenticated);
        }

        [Test]
        public void RuntimeSettingsExposeTheDevelopmentRoomPolicy()
        {
            OnlineServicesSettings runtime = OnlineServicesSettings.RuntimeDefault;
            Assert.AreEqual("production", runtime.EnvironmentName);
            Assert.AreEqual(10, runtime.MaxPlayers);
            Assert.AreEqual(5, runtime.MaxPlayersPerTeam);
            Assert.AreEqual(24, runtime.ProtocolVersion);
            Assert.IsTrue(runtime.AllowDirectDevelopmentNetworking);
        }

        [Test]
        public void RelayFrontendHandoffRetainsHeroAndDoesNotExposeAnAddress()
        {
            FrontendLaunchRequest.Set(FrontendMatchMode.RelayClient, TeamId.Ember, "relay", 0, "tempest_arbiter");
            Assert.IsTrue(FrontendLaunchRequest.TryConsume(out FrontendMatchMode mode, out TeamId team, out string address, out _, out string hero));
            Assert.AreEqual(FrontendMatchMode.RelayClient, mode);
            Assert.AreEqual(TeamId.Ember, team);
            Assert.AreEqual("relay", address);
            Assert.AreEqual("tempest_arbiter", hero);
        }

        private MatchPlayerSlot Player(string id, TeamId team) => new MatchPlayerSlot
        {
            PlayerId = id,
            DisplayName = "Jugador " + id,
            Team = team,
            IsConnected = true,
            HeroId = "storm_warden",
            BuildVersion = settings.BuildVersion,
            ProtocolVersion = settings.ProtocolVersion
        };
    }
}
