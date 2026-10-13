using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>Moves the shooter camera through the start screen's wide shots (StartShots). Leaving the start screen gives
    /// the camera back with the game's field of view; BreakawayView then places it (the Round's start position).</summary>
    public sealed class StartShotsView
    {
        readonly Camera _camera;
        readonly float _gameFieldOfView;

        public StartShotsView(Camera camera, float gameFieldOfView)
        {
            _camera = camera;
            _gameFieldOfView = gameFieldOfView;
        }

        /// <summary>One frame: onStartScreen is flow.Screen == FlowScreen.Start (not the Round's phase: after a Round it is
        /// RoundOver on the start screen). realDt in real seconds.</summary>
        public void Step(bool onStartScreen, float realDt)
        {
            if (!onStartScreen)
            {
                if (Showing) _camera.fieldOfView = _gameFieldOfView;
                Showing = false;
                return;
            }

            if (!Showing) SecondsOnStartScreen = 0;
            Showing = true;
            SecondsOnStartScreen += realDt;

            StartShotPose pose = StartShots.At(SecondsOnStartScreen);
            Transform t = _camera.transform;
            t.position = new Vector3((float)pose.Eye.X, (float)pose.Eye.Y, (float)pose.Eye.Z);
            t.LookAt(new Vector3((float)pose.Look.X, (float)pose.Look.Y, (float)pose.Look.Z));
            _camera.fieldOfView = (float)pose.FieldOfViewDegrees;
        }

        public double SecondsOnStartScreen { get; private set; }
        public bool Showing { get; private set; } // true while it drives the camera
    }
}
