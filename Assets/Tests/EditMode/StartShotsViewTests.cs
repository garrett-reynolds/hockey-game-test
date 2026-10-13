using HockeyGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    public class StartShotsViewTests
    {
        Camera _cam;

        [SetUp]
        public void Build()
        {
            _cam = new GameObject("Camera").AddComponent<Camera>();
            _cam.fieldOfView = 60f;
        }

        [TearDown]
        public void Remove() => Object.DestroyImmediate(_cam.gameObject);

        [Test]
        public void On_the_start_screen_the_camera_is_at_the_wide_shot_with_its_wider_field_of_view()
        {
            var view = new StartShotsView(_cam, 60f);
            view.Step(true, 1.0f);
            StartShotPose pose = StartShots.At(1.0);
            Assert.That(Vector3.Distance(_cam.transform.position, new Vector3((float)pose.Eye.X, (float)pose.Eye.Y, (float)pose.Eye.Z)), Is.LessThan(1e-3f));
            Assert.AreEqual((float)pose.FieldOfViewDegrees, _cam.fieldOfView, 1e-4f);
        }

        [Test]
        public void Leaving_the_start_screen_puts_the_games_field_of_view_back_on_that_frame()
        {
            var view = new StartShotsView(_cam, 60f);
            view.Step(true, 3f);
            view.Step(false, 0.016f);
            Assert.AreEqual(60f, _cam.fieldOfView, 1e-6f);
            Assert.IsFalse(view.Showing);
        }

        [Test]
        public void Each_return_to_the_start_screen_begins_at_shot_0()
        {
            var view = new StartShotsView(_cam, 60f);
            view.Step(true, 20f); view.Step(false, 0.1f); view.Step(true, 0.5f);
            Assert.AreEqual(0.5, view.SecondsOnStartScreen, 1e-6);
        }

        [Test]
        public void A_press_on_the_start_screen_after_a_Round_puts_the_camera_at_the_Rounds_start_position()
        {
            var flow = new ScreenFlow(ScriptedRandom.Halves(), new SaveData(), FakeReach.NeverReaches());
            flow.Press();
            for (int shot = 0; shot < Round.ShotsPerRound; shot++)
            {
                flow.Tick(3.7);
                flow.SubmitShot(-10, 50, 100, false);
                flow.Round.WhistleNow();
                flow.Tick(flow.Round.ResultHoldLength);
            }
            flow.Tick(1000); // past AutoReturnSeconds: the start screen, the Round still RoundOver
            Assert.AreEqual(FlowScreen.Start, flow.Screen);

            var view = new StartShotsView(_cam, 60f);
            view.Step(flow.Screen == FlowScreen.Start, 2f);
            Assert.IsTrue(view.Showing);

            flow.Press();
            view.Step(flow.Screen == FlowScreen.Start, 0.016f);
            Assert.IsFalse(view.Showing);
            Assert.AreEqual(60f, _cam.fieldOfView, 1e-6f);

            Breakaway.SkatePoint(flow.Round.AttackSide, 0, out double x, out double d);
            BreakawayView.PlaceShooterCamera(_cam.transform, (float)x, (float)d, 0f, 0f);
            Assert.That(_cam.transform.position.x, Is.EqualTo((float)x).Within(0.07f));
            Assert.That(_cam.transform.position.y, Is.EqualTo((float)Breakaway.EyeHeight).Within(0.07f));
            Assert.That(_cam.transform.position.z, Is.EqualTo(-(float)d).Within(0.07f));
        }
    }
}
