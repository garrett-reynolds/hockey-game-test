using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The Replay's Unity side (#91): the recorded Shot plays back and gives the live scene back; the shooter's camera looks up clear of the net.</summary>
    public class ReplayViewTests
    {
        GameObject _goalie, _puck, _eye;

        [SetUp]
        public void Make()
        {
            _goalie = new GameObject("Goalie");
            new GameObject("Glove").transform.SetParent(_goalie.transform, false);
            _puck = new GameObject("Puck");
            _eye = new GameObject("Shooter Eye");
        }

        [TearDown]
        public void Remove()
        {
            Object.DestroyImmediate(_goalie);
            Object.DestroyImmediate(_puck);
            Object.DestroyImmediate(_eye);
        }

        Transform Glove => _goalie.transform.GetChild(0);

        /// <summary>Records the goalie and puck at 0 s and 1 s: the glove reaches up 1 m, the puck flies 10 m and goes out of sight.</summary>
        ReplayTrack Recorded()
        {
            var track = new ReplayTrack();
            track.Begin(_goalie.transform, _puck.transform);
            _puck.transform.position = new Vector3(0, 0.5f, -10);
            track.Record(0);
            Glove.localPosition = Vector3.up;
            _puck.transform.position = new Vector3(0, 0.5f, 0);
            _puck.SetActive(false);
            track.Record(1);
            return track;
        }

        [Test]
        public void The_track_holds_every_part_under_the_ones_listed()
        {
            ReplayTrack track = Recorded();
            Assert.AreEqual(3, track.PartCount, "goalie, glove, puck");
            Assert.AreEqual(2, track.SampleCount);
        }

        [Test]
        public void The_Replay_shows_the_recorded_moment_blending_between_frames()
        {
            ReplayTrack track = Recorded();
            _puck.transform.position = new Vector3(5, 5, 5); // the live scene has moved on
            Glove.localPosition = Vector3.zero;
            track.Apply(0.25);
            Assert.AreEqual(-7.5f, _puck.transform.position.z, 1e-4f);
            Assert.AreEqual(0.25f, Glove.localPosition.y, 1e-4f);
            Assert.IsTrue(_puck.activeSelf, "in sight as it was at 0 s");
            track.Apply(2.0);
            Assert.AreEqual(0f, _puck.transform.position.z, 1e-4f, "held at the last moment");
            Assert.IsFalse(_puck.activeSelf);
        }

        [Test]
        public void Restore_gives_the_live_scene_back()
        {
            ReplayTrack track = Recorded();
            _puck.SetActive(true);
            _puck.transform.position = new Vector3(5, 5, 5);
            Glove.localPosition = Vector3.zero;
            track.Apply(0.5);
            track.Apply(0.75);
            Assert.IsTrue(track.Applied);
            track.Restore();
            Assert.IsFalse(track.Applied);
            Assert.AreEqual(new Vector3(5, 5, 5), _puck.transform.position);
            Assert.AreEqual(Vector3.zero, Glove.localPosition);
            Assert.IsTrue(_puck.activeSelf);
        }

        [Test]
        public void Nothing_recorded_changes_nothing()
        {
            var track = new ReplayTrack();
            track.Begin(_puck.transform);
            _puck.transform.position = Vector3.one;
            track.Apply(0.5);
            Assert.IsFalse(track.Applied);
            Assert.AreEqual(Vector3.one, _puck.transform.position);
        }

        // ---- the cameras ----

        [Test]
        public void Looking_up_at_the_Video_Board_keeps_the_net_and_goalie_out_of_the_shooters_view()
        {
            var camera = _eye.AddComponent<Camera>(); // the game's camera: Unity's default 60° tall view
            camera.aspect = 16f / 9f;
            var settings = new RoundSettings();
            Vector3 board = new Vector3((float)RinkLayout.VideoBoard.X, (float)RinkLayout.VideoBoard.Y, (float)RinkLayout.VideoBoard.Z);
            float halfW = (float)RinkLayout.VideoBoardWidth / 2f, halfH = (float)RinkLayout.VideoBoardHeight / 2f;
            foreach (AttackSide side in new[] { AttackSide.Left, AttackSide.Middle, AttackSide.Right })
            foreach (double window in new[] { 0.0, settings.ShootingWindowSeconds })
            for (float game = 0f; game < 1.3f; game += 0.05f) // every stride bob and roll
            {
                Breakaway.SkatePoint(side, 1, out double x, out double distance);
                float drift = (float)Breakaway.Drift(side, settings.SlowDownSeconds, window, settings);
                BreakawayView.PlaceShooterCamera(_eye.transform, (float)x, (float)distance, drift, game);
                BreakawayView.LookUpTo(_eye.transform, BreakawayView.ReplayLookTarget, 1f);
                string at = side + ", window " + window + " s, bob " + game;
                foreach (Vector3 corner in new[] { new Vector3(-halfW, -halfH, 0), new Vector3(halfW, -halfH, 0), new Vector3(-halfW, halfH, 0), new Vector3(halfW, halfH, 0) })
                {
                    Vector3 v = camera.WorldToViewportPoint(board + corner);
                    Assert.IsTrue(v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1, at + ": board corner " + corner + " in view, at " + v);
                }
                foreach (Vector3 p in new[] { new Vector3(-0.92f, 1.22f, 0f), new Vector3(0.92f, 1.22f, 0f), new Vector3(0f, 2.0f, -1.2f) })
                    Assert.Less(camera.WorldToViewportPoint(p).y, 0f, at + ": " + p + " is out of view");
            }
        }

        [Test]
        public void Half_way_up_the_camera_is_half_way_between_the_net_and_the_board()
        {
            Vector3 board = new Vector3(0, 16, 16);
            BreakawayView.PlaceShooterCamera(_eye.transform, 0f, 9f, 0f, 0f);
            Quaternion onNet = _eye.transform.rotation;
            BreakawayView.LookUpTo(_eye.transform, board, 0f);
            Assert.AreEqual(0f, Quaternion.Angle(onNet, _eye.transform.rotation), 1e-3f, "no look up: on the net");
            BreakawayView.LookUpTo(_eye.transform, board, 0.5f);
            float toNet = Quaternion.Angle(onNet, _eye.transform.rotation);
            float toBoard = Quaternion.Angle(Quaternion.LookRotation(board - _eye.transform.position), _eye.transform.rotation);
            Assert.AreEqual(toNet, toBoard, 0.05f);
        }

        [Test]
        public void The_Replay_camera_sits_behind_the_net_looking_back_at_the_shooter_with_the_goalie_in_view()
        {
            Assert.Greater(ReplayView.CameraSpot.z, 1.2f, "behind the net's back");
            Assert.Less(ReplayView.CameraSpot.z, (float)RinkLayout.NearEndBoardsZ, "inside the end boards");
            var camera = _eye.AddComponent<Camera>();
            camera.fieldOfView = ReplayView.FieldOfView;
            camera.aspect = ReplayView.PictureWidth / (float)ReplayView.PictureHeight;
            foreach (float shooterX in new[] { -3f, 0f, 3f })
            {
                var shooter = new Vector3(shooterX, 1.5f, -6f);
                _eye.transform.SetPositionAndRotation(ReplayView.CameraSpot, Quaternion.LookRotation(ReplayView.AimPoint(shooter) - ReplayView.CameraSpot));
                foreach (Vector3 p in new[] { shooter, new Vector3(0f, 0.1f, -0.6f), new Vector3(0f, 1.9f, -0.6f), new Vector3(-0.9f, 0.5f, -0.6f), new Vector3(0.9f, 0.5f, -0.6f) })
                {
                    Vector3 v = camera.WorldToViewportPoint(p);
                    Assert.IsTrue(v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1, "shooter " + shooterX + ": " + p + " in view, at " + v);
                }
            }
        }
    }
}
