using CierzoArena.Units;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    public sealed class ExpandedScoreboardTests
    {
        [Test]
        public void ExpandedScoreboardSupportsFiveStableRowsPerTeam()
        {
            Assert.That(MatchScoreboardController.RowsPerTeam,Is.EqualTo(5));
            Assert.That(MatchScoreboardController.StableSlotForHeroId(1000),Is.EqualTo(0));
            Assert.That(MatchScoreboardController.StableSlotForHeroId(1004),Is.EqualTo(4));
            Assert.That(MatchScoreboardController.StableSlotForHeroId(2003),Is.EqualTo(3));
        }
        [Test]
        public void ScoreboardVisibilityUsesHeldInputOrFinalMatchState()
        {
            Assert.That(MatchScoreboardController.ShouldShowScoreboard(false,false),Is.False);
            Assert.That(MatchScoreboardController.ShouldShowScoreboard(false,true),Is.True);
            Assert.That(MatchScoreboardController.ShouldShowScoreboard(true,false),Is.False);
        }
        [Test]
        public void HeaderAndEveryRowShareOneEightColumnGeometry()
        {
            Rect header=new Rect(30f,40f,1000f,24f);
            Rect azureRow=new Rect(30f,70f,1000f,32f);
            Rect emberRow=new Rect(30f,110f,1000f,32f);
            float accumulated=0f;
            for(int i=0;i<ScoreboardColumnLayout.ColumnCount;i++)
            {
                ScoreboardColumn column=(ScoreboardColumn)i;
                Assert.That(ScoreboardColumnLayout.GetHeader(column),Is.Not.Empty);
                Assert.That(ScoreboardColumnLayout.GetCell(header,column).xMin,Is.EqualTo(ScoreboardColumnLayout.GetCell(azureRow,column).xMin));
                Assert.That(ScoreboardColumnLayout.GetCell(header,column).width,Is.EqualTo(ScoreboardColumnLayout.GetCell(azureRow,column).width));
                Assert.That(ScoreboardColumnLayout.GetCell(azureRow,column).xMin,Is.EqualTo(ScoreboardColumnLayout.GetCell(emberRow,column).xMin));
                Assert.That(ScoreboardColumnLayout.GetCell(azureRow,column).width,Is.EqualTo(ScoreboardColumnLayout.GetCell(emberRow,column).width));
                accumulated+=ScoreboardColumnLayout.GetWidthFraction(column);
            }
            Assert.That(ScoreboardColumnLayout.ColumnCount,Is.EqualTo(8));
            Assert.That(ScoreboardColumnLayout.GetHeader(ScoreboardColumn.Gold),Is.EqualTo("ORO"));
            Assert.That(accumulated,Is.EqualTo(1f).Within(.0001f));
        }
        [Test]
        public void GoldVisibilityFollowsTheActualLocalTeam()
        {
            Assert.That(MatchScoreboardController.ShouldRevealGold(CierzoArena.Core.TeamId.Azure,CierzoArena.Core.TeamId.Azure),Is.True);
            Assert.That(MatchScoreboardController.ShouldRevealGold(CierzoArena.Core.TeamId.Azure,CierzoArena.Core.TeamId.Ember),Is.False);
            Assert.That(MatchScoreboardController.ShouldRevealGold(CierzoArena.Core.TeamId.Ember,CierzoArena.Core.TeamId.Ember),Is.True);
            Assert.That(MatchScoreboardController.ShouldRevealGold(CierzoArena.Core.TeamId.Ember,CierzoArena.Core.TeamId.Azure),Is.False);
        }
    }
}
