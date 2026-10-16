using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class PauseClockTests
    {
        [Test]
        public void The_clock_follows_real_time_until_it_is_paused()
        {
            var clock = new PauseClock();
            Assert.AreEqual(10.0, clock.Now(10.0), 1e-9);
            Assert.AreEqual(12.5, clock.Now(12.5), 1e-9);
        }

        [Test]
        public void The_clock_stands_still_while_paused_and_carries_on_from_the_same_moment_after_Resume()
        {
            var clock = new PauseClock();
            clock.Pause(10.0);
            Assert.AreEqual(10.0, clock.Now(10.0), 1e-9);
            Assert.AreEqual(10.0, clock.Now(15.0), 1e-9, "5 s paused do not count");
            clock.Resume(15.0);
            Assert.AreEqual(10.0, clock.Now(15.0), 1e-9, "Resume carries on from the same moment");
            Assert.AreEqual(11.0, clock.Now(16.0), 1e-9);
        }

        [Test]
        public void Two_pauses_both_drop_out_and_a_repeated_Pause_or_Resume_changes_nothing()
        {
            var clock = new PauseClock();
            clock.Resume(1.0); // not paused: nothing
            clock.Pause(10.0);
            clock.Pause(12.0); // already paused: the first Pause stands
            clock.Resume(14.0);
            clock.Pause(20.0);
            clock.Resume(23.0);
            Assert.AreEqual(25.0 - 4.0 - 3.0, clock.Now(25.0), 1e-9);
        }
    }

    public class SavedFadeTests
    {
        [Test]
        public void SAVED_is_solid_then_fades_over_the_end_of_its_time()
        {
            Assert.AreEqual(1.0, SavedFade.Opacity(SavedFade.SavedSeconds), 1e-9);
            Assert.AreEqual(1.0, SavedFade.Opacity(SavedFade.FadeSeconds + 0.01), 1e-9);
            Assert.AreEqual(0.5, SavedFade.Opacity(SavedFade.FadeSeconds / 2), 1e-9);
            Assert.AreEqual(0.0, SavedFade.Opacity(0), 1e-9);
            Assert.AreEqual(0.0, SavedFade.Opacity(-1), 1e-9);
        }
    }
}
