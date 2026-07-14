using CierzoArena.Core;
using NUnit.Framework;

namespace CierzoArena.Tests.Editor
{
    public sealed class MatchNavigationStateTests
    {
        [SetUp]
        public void SetUp() => MatchNavigationState.CompleteExit();

        [TearDown]
        public void TearDown() => MatchNavigationState.CompleteExit();

        [Test]
        public void OpeningMenuKeepsMatchActiveButBlocksGameplayInput()
        {
            MatchNavigationState.BeginMatch(true, false, true);
            MatchNavigationState.OpenMainMenu();

            Assert.That(MatchNavigationState.IsMatchActive, Is.True);
            Assert.That(MatchNavigationState.IsMainMenuVisible, Is.True);
            Assert.That(MatchNavigationState.IsGameplayInputAllowed, Is.False);
            Assert.That(MatchNavigationState.CanReturnToMatch, Is.True);
        }

        [Test]
        public void ReturnRestoresTheExistingGameplayView()
        {
            MatchNavigationState.BeginMatch(false, false, false);
            MatchNavigationState.OpenMainMenu();
            MatchNavigationState.ReturnToMatch();

            Assert.That(MatchNavigationState.IsMatchActive, Is.True);
            Assert.That(MatchNavigationState.IsInGameplayView, Is.True);
            Assert.That(MatchNavigationState.IsMainMenuVisible, Is.False);
            Assert.That(MatchNavigationState.IsGameplayInputAllowed, Is.True);
        }

        [Test]
        public void OpeningMenuIsViewOnlyAndNeverRequestsDisconnect()
        {
            bool disconnectRequested = false;
            void RecordDisconnect() => disconnectRequested = true;

            MatchNavigationState.DisconnectRequested += RecordDisconnect;
            try
            {
                MatchNavigationState.BeginMatch(false, false, false);
                MatchNavigationState.OpenMainMenu();

                Assert.That(disconnectRequested, Is.False);
                Assert.That(MatchNavigationState.IsLocalMatch, Is.True);
                Assert.That(MatchNavigationState.CurrentMatchPhase, Is.EqualTo("Menú sobre partida activa"));
            }
            finally
            {
                MatchNavigationState.DisconnectRequested -= RecordDisconnect;
            }
        }

        [Test]
        public void FinishedMatchCannotReturnFromMenu()
        {
            MatchNavigationState.BeginMatch(true, true, false);
            MatchNavigationState.OpenMainMenu();
            MatchNavigationState.MarkMatchFinished();
            MatchNavigationState.ReturnToMatch();

            Assert.That(MatchNavigationState.IsMatchFinished, Is.True);
            Assert.That(MatchNavigationState.CanReturnToMatch, Is.False);
            Assert.That(MatchNavigationState.IsInGameplayView, Is.False);
        }

        [Test]
        public void DisconnectIntentPreservesRoleUntilOwnerCompletesExit()
        {
            MatchNavigationState.BeginMatch(true, true, false);
            MatchNavigationState.RequestDisconnect();

            Assert.That(MatchNavigationState.IsDisconnecting, Is.True);
            Assert.That(MatchNavigationState.IsHost, Is.True);
            Assert.That(MatchNavigationState.IsGameplayInputAllowed, Is.False);
        }
    }
}
