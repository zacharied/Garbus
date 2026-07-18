// Model tests for DesignPointInfo: sorted insertion, structural moves, and the single change event.
// Plain NUnit — no game host required.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts.Design;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts
{
    [TestFixture]
    public class TestDesignPointInfo
    {
        [Test]
        public void TestAddKeepsSortedOrder()
        {
            var info = new DesignPointInfo();
            info.Add(new TutorialMessage { StartTime = 3000, EndTime = 4000 });
            info.Add(new TutorialMessage { StartTime = 1000, EndTime = 2000 });
            info.Add(new TutorialMessage { StartTime = 2000, EndTime = 2500 });

            Assert.That(info.DesignPoints.Select(p => p.StartTime), Is.EqualTo(new[] { 1000.0, 2000.0, 3000.0 }));
        }

        [Test]
        public void TestAddRaisesChangeEvent()
        {
            var info = new DesignPointInfo();
            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            info.Add(new TutorialMessage { StartTime = 0, EndTime = 100 });

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void TestMoveReordersAndRaisesEvent()
        {
            var info = new DesignPointInfo();
            var a = new TutorialMessage { StartTime = 1000, EndTime = 1500 };
            var b = new TutorialMessage { StartTime = 2000, EndTime = 2500 };
            info.Add(a);
            info.Add(b);

            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            info.MoveDesignPoint(a, 3000, 3500);

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(a.StartTime, Is.EqualTo(3000));
            Assert.That(a.EndTime, Is.EqualTo(3500));
            Assert.That(info.DesignPoints.Select(p => p.StartTime), Is.EqualTo(new[] { 2000.0, 3000.0 }));
        }

        [Test]
        public void TestTextSetDoesNotRaiseOrReorder()
        {
            var info = new DesignPointInfo();
            var a = new TutorialMessage { StartTime = 1000, EndTime = 1500, Text = "old" };
            info.Add(a);

            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            a.Text = "new"; // in-place edit of an effect parameter, not a structural change

            Assert.That(raised, Is.EqualTo(0));
            Assert.That(info.DesignPoints.Single().StartTime, Is.EqualTo(1000));
        }

        [Test]
        public void TestRenderTranslatesNewlineEscapes()
        {
            Assert.That(TutorialMessage.Render("line1\\nline2"), Is.EqualTo("line1\nline2"));
        }

        [Test]
        public void TestRenderLeavesOtherBackslashesUntouched()
        {
            // Only the two-char \n sequence is translated; a lone backslash or \t passes through.
            Assert.That(TutorialMessage.Render("a\\tb\\c"), Is.EqualTo("a\\tb\\c"));
        }

        [Test]
        public void TestRemoveAndClearRaiseEvent()
        {
            var info = new DesignPointInfo();
            var a = new TutorialMessage { StartTime = 0, EndTime = 100 };
            info.Add(a);

            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            info.Remove(a);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(info.DesignPoints, Is.Empty);

            info.Add(new TutorialMessage { StartTime = 0, EndTime = 100 });
            raised = 0;
            info.Clear();
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(info.DesignPoints, Is.Empty);
        }
    }
}
