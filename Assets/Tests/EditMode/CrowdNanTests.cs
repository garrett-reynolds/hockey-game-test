using HockeyGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// A NaN pixel is bloomed into a big white disc (URP's bloom prefilter min(ClampMax, NaN) keeps the NaN), and the fans moving
    /// change which pixels it is: "flashing lights over the stands" (2026-10-13). So the crowd must draw no NaN or Inf.
    /// </summary>
    public class CrowdNanTests
    {
        const int Width = 960, Height = 540;
        GameObject _rink;
        Camera _camera;
        RenderTexture _target;
        Texture2D _texture;

        [TearDown]
        public void Clean()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_rink != null) RinkTeardown.Destroy(_rink);
            if (_target != null) Object.DestroyImmediate(_target);
            if (_texture != null) Object.DestroyImmediate(_texture);
        }

        [Test]
        [Category("Slow"), Explicit("Slow: runs only in the full run (--include_explicit true)")]
        public void The_crowd_in_the_shooter_view_draws_no_NaN_or_Inf_pixel_in_a_float_target()
        {
            _rink = RinkBuilder.Build(out Camera camera, null).gameObject;
            _camera = camera;
            var crowd = _rink.GetComponentInChildren<Crowd>();
            Assert.IsNotNull(crowd, "the crowd body files are in the project");
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; // the raw frame: bloom would hide the NaN in its own way
            camera.allowHDR = true;
            camera.aspect = Width / (float)Height;
            camera.fieldOfView = 60f;
            Breakaway.SkatePoint(AttackSide.Middle, 1.0, out double x, out double distance);
            BreakawayView.PlaceShooterCamera(camera.transform, (float)x, (float)distance, 0f, 0f);
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            _texture = new Texture2D(Width, Height, TextureFormat.RGBAHalf, false);
            camera.targetTexture = _target;

            var views = new[]
            {
                (Phase: RoundPhase.Idle, Result: ArenaResult.None, Seconds: 1.0),
                (Phase: RoundPhase.ResultHold, Result: ArenaResult.Goal, Seconds: 8.0)
            };
            foreach (var view in views)
            {
                ArenaState state = ArenaShow.At(new ArenaShowInput { Phase = view.Phase, PhaseSeconds = 1.0, Result = view.Result, ShotNumber = 3 });
                crowd.Show(state, view.Seconds - 0.1);
                crowd.Show(state, view.Seconds);
                camera.Render();
                RenderTexture.active = _target;
                _texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                _texture.Apply();
                RenderTexture.active = null;
                Assert.That(crowd.LastDrawnFans, Is.GreaterThan(1000), "the crowd was drawn in the picture");

                int bad = 0;
                foreach (Color c in _texture.GetPixels())
                    if (!float.IsFinite(c.r) || !float.IsFinite(c.g) || !float.IsFinite(c.b)) bad++;
                Assert.AreEqual(0, bad, view.Phase + " " + view.Result + ": pixels that are NaN or Inf (each blooms into a white blob)");
            }
        }
    }
}
