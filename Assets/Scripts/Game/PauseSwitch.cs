using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The one pause switch (#196). Set(true) stops game time (Time.timeScale 0: physics, animation, particles) and all sound
    /// (AudioListener.pause). The parts that run on real time ask IsPaused and stand still: RoundDriver.Update, BreakawayView and ReplayView
    /// (camera moves, the goalie's stance life, the Replay), and the Round clock itself (ScreenFlow.Paused ignores Tick).
    /// Set(false) puts the time scale back to what it was.
    /// </summary>
    public static class PauseSwitch
    {
        static float _savedTimeScale = 1f;

        static int _pausedFrame = -1;

        public static bool IsPaused { get; private set; }

        static HockeyGame.Core.PauseClock _clock = new HockeyGame.Core.PauseClock();
        static HockeyGame.Core.PauseClock _drawnClock = new HockeyGame.Core.PauseClock();

        /// <summary>Real-time seconds that stand still while paused (for the ribbons, boards and crowd): Resume carries on from the same moment.</summary>
        public static double Seconds => _clock.Now(Time.unscaledTimeAsDouble);

        /// <summary>Final-draw wall time, with paused spans removed. Game-frame delta never drives this clock.</summary>
        public static double DrawnSeconds => _drawnClock.Now(Time.realtimeSinceStartupAsDouble);

        /// <summary>
        /// True from the frame after Set(true). The views skip their LateUpdate on these frames only: the frame the pause began still
        /// poses the goalie and camera (Update had already put last frame's live poses back), so the picture under the box is whole.
        /// </summary>
        public static bool StandStill => IsPaused && Time.frameCount != _pausedFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            IsPaused = false; // a Play with domain reload off must not start paused
            _clock = new HockeyGame.Core.PauseClock();
            _drawnClock = new HockeyGame.Core.PauseClock();
        }

        public static void Set(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            if (paused) _clock.Pause(Time.unscaledTimeAsDouble);
            else _clock.Resume(Time.unscaledTimeAsDouble);
            if (paused) _drawnClock.Pause(Time.realtimeSinceStartupAsDouble);
            else _drawnClock.Resume(Time.realtimeSinceStartupAsDouble);
            if (paused)
            {
                _pausedFrame = Time.frameCount;
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            }
            AudioListener.pause = paused;
        }
    }
}
