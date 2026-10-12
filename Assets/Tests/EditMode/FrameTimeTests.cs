using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    /// <summary>The frame lock and the dev-only frame readout (#85). Their effect on screen is checked in the Editor.</summary>
    public class FrameTimeTests
    {
        [Test]
        public void The_game_locks_to_60_fps_with_VSync_on()
        {
            Assert.AreEqual(60, FrameRate.TargetFps);
            Assert.AreEqual(1, FrameRate.VSyncCount, "one vertical blank per frame: VSync on");
        }

        [Test]
        public void The_readout_shows_CPU_and_GPU_milliseconds_to_one_decimal()
        {
            Assert.AreEqual("CPU 4.2 ms   GPU 7.9 ms", FrameReadout.Text(4.24, 7.91));
        }

        [Test]
        public void The_readout_says_n_a_when_a_time_is_not_measured()
        {
            Assert.AreEqual("CPU 4.2 ms   GPU n/a", FrameReadout.Text(4.24, 0));
            Assert.AreEqual("CPU n/a   GPU n/a", FrameReadout.Text(double.NaN, -1));
        }

        [Test]
        public void The_readout_shows_only_in_the_Editor_and_development_builds()
        {
            Assert.IsTrue(FrameReadout.Shows(true));
            Assert.IsFalse(FrameReadout.Shows(false), "a normal (release) build hides it");
        }
    }
}
