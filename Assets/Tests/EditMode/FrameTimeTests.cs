using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The frame lock and the dev-only frame readout (#85). Their effect on screen is checked in the Editor.</summary>
    public class FrameTimeTests
    {
        int _vSyncCount, _targetFrameRate;

        [SetUp]
        public void RememberFrameSettings()
        {
            _vSyncCount = QualitySettings.vSyncCount;
            _targetFrameRate = Application.targetFrameRate;
        }

        [TearDown]
        public void RestoreFrameSettings()
        {
            QualitySettings.vSyncCount = _vSyncCount;
            Application.targetFrameRate = _targetFrameRate;
        }

        [Test]
        public void Lock_turns_VSync_on_and_caps_at_60_fps_on_a_60_Hz_screen()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            FrameRate.Lock(60);
            Assert.AreEqual(1, QualitySettings.vSyncCount);
            Assert.AreEqual(60, Application.targetFrameRate);
        }

        [Test]
        public void Lock_waits_two_vertical_blanks_a_frame_on_a_120_Hz_screen_so_it_still_runs_at_60()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            FrameRate.Lock(120);
            Assert.AreEqual(2, QualitySettings.vSyncCount);
            Assert.AreEqual(60, Application.targetFrameRate);
        }

        [Test]
        public void Vertical_blanks_per_frame_follow_the_refresh_rate()
        {
            Assert.AreEqual(1, FrameRate.VSyncCountFor(60));
            Assert.AreEqual(1, FrameRate.VSyncCountFor(59.94));
            Assert.AreEqual(2, FrameRate.VSyncCountFor(120));
            Assert.AreEqual(2, FrameRate.VSyncCountFor(144));
            Assert.AreEqual(4, FrameRate.VSyncCountFor(240));
        }

        [Test]
        public void An_unknown_slow_or_very_fast_refresh_rate_still_gets_VSync_on()
        {
            Assert.AreEqual(1, FrameRate.VSyncCountFor(0), "refresh rate not reported");
            Assert.AreEqual(1, FrameRate.VSyncCountFor(30));
            Assert.AreEqual(4, FrameRate.VSyncCountFor(360), "Unity waits at most 4 vertical blanks");
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
        public void A_normal_build_gets_no_readout_and_a_development_build_gets_one()
        {
            var normal = new GameObject("Round Driver (normal build)");
            var development = new GameObject("Round Driver (development build)");
            try
            {
                FrameReadout.AddTo(normal, false);
                FrameReadout.AddTo(development, true);
                Assert.IsNull(normal.GetComponent<FrameReadout>(), "hidden in normal play");
                Assert.IsNotNull(development.GetComponent<FrameReadout>(), "shown in the Editor and development builds");
            }
            finally
            {
                Object.DestroyImmediate(normal);
                Object.DestroyImmediate(development);
            }
        }
    }
}
