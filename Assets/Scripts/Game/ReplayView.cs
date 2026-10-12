using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HockeyGame
{
    /// <summary>
    /// The Replay (#91): after a Goal, a Save or a Frame Hit it records what is seen of the Shot (ReplayTrack), and while the
    /// Arena Show says the Replay plays it shows that Shot again in slow motion (ReplayTiming) to a second camera behind the
    /// near net looking back at the shooter. That camera draws a small render texture that both boards show. The camera
    /// draws only while the Replay plays. The recorded poses go back to the live ones at the start of the next frame
    /// (RestoreLive, from RoundDriver) so the game's own views carry on from where they were. Runs after the views'
    /// LateUpdate (BreakawayView), so it records and shows what they set.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class ReplayView : MonoBehaviour
    {
        /// <summary>The Replay picture's size: small (the board budget, cut order item 2), near the Video Board's 11 x 6 shape.</summary>
        public const int PictureWidth = 512, PictureHeight = 280;
        /// <summary>The Replay camera: behind the near net, above the crossbar, inside the end boards.</summary>
        public static readonly Vector3 CameraSpot = new Vector3(0f, 2.4f, 2.6f);
        public const float FieldOfView = 45f;
        /// <summary>Real seconds the recording keeps going after the clip's end, so the last moment is always there.</summary>
        const double RecordMargin = 0.1;

        Round _round;
        VideoBoards _boards;
        Camera _shooter;
        Transform[] _parts;
        readonly ReplayTrack _track = new ReplayTrack();
        ArenaState _show;
        bool _recording, _begun, _showing;

        /// <summary>The camera that draws the Replay (off unless the Replay plays).</summary>
        public Camera ReplayCamera { get; private set; }
        /// <summary>The Replay picture both boards show.</summary>
        public RenderTexture Picture { get; private set; }
        public bool Showing => _showing;
        /// <summary>Moments recorded of the last Shot that gets a Replay (0 after a Wide Miss or Too late).</summary>
        public int RecordedMoments => _track.SampleCount;

        /// <summary>
        /// Sets the Replay up. parts: what the Replay plays back (the goalie, the save block, then the puck last, since the puck
        /// can ride in the glove). shooter: the player's camera, copied for the Replay camera's look.
        /// </summary>
        public void Init(Round round, VideoBoards boards, Camera shooter, params Transform[] parts)
        {
            _round = round;
            _boards = boards;
            _shooter = shooter;
            _parts = parts;
            Picture = new RenderTexture(PictureWidth, PictureHeight, 24) { name = "Replay Picture", antiAliasing = 1 };
            var cameraObject = new GameObject("Replay Camera");
            cameraObject.transform.SetParent(transform, false);
            ReplayCamera = cameraObject.AddComponent<Camera>();
            ReplayCamera.CopyFrom(shooter);
            ReplayCamera.fieldOfView = FieldOfView;
            ReplayCamera.targetTexture = Picture;
            ReplayCamera.aspect = PictureWidth / (float)PictureHeight;
            ReplayCamera.depth = shooter.depth - 1;
            var data = ReplayCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; // no bloom, grade or slow-down look: within the board budget
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            ReplayCamera.enabled = false;
            ReplayCamera.transform.position = CameraSpot;
        }

        /// <summary>The Arena Show's answer this frame: the Replay plays while its camera mode is Replay.</summary>
        public void Show(ArenaState show) => _show = show;

        /// <summary>Puts the live poses back after a frame that showed the Replay. Call first thing each frame.</summary>
        public void RestoreLive() => _track.Restore();

        void LateUpdate()
        {
            if (_round != null) Step();
        }

        /// <summary>One frame, after the views have posed the scene: records the Shot, and plays the Replay while the Arena Show says so.</summary>
        public void Step()
        {
            Record();
            bool play = _show.Camera == CameraMode.Replay && _track.SampleCount > 0;
            if (play) _track.Apply(ReplayTiming.ShotSecondsAt(_show.ReplaySeconds, _round.ResultDelaySeconds));
            SetShowing(play);
        }

        /// <summary>From each Shot that gets a Replay, records its clip (from the Shot to just after the clip ends).</summary>
        void Record()
        {
            bool hold = _round.Phase == RoundPhase.ResultHold && ReplayTiming.Follows(_round.LastResult);
            if (!hold)
            {
                if (_round.Phase == RoundPhase.ResultHold && !_begun) { _track.Begin(); _begun = true; } // no Replay: drop the last one
                if (_round.Phase != RoundPhase.ResultHold) _begun = false;
                _recording = false;
                return;
            }
            double seconds = _round.PhaseSecondsElapsed;
            if (!_begun)
            {
                _begun = _recording = true;
                _track.Begin(_parts);
                Aim(_shooter.transform.position);
            }
            if (!_recording) return;
            _track.Record(seconds);
            if (_round.ResultShowing && seconds > ReplayTiming.ClipEnd(_round.ResultDelaySeconds) + RecordMargin) _recording = false;
        }

        /// <summary>Points the Replay camera from behind the net back up the ice at the shooter, framing the goalie and the puck's path.</summary>
        void Aim(Vector3 shooterEye) => ReplayCamera.transform.SetPositionAndRotation(CameraSpot, Quaternion.LookRotation(AimPoint(shooterEye) - CameraSpot));

        /// <summary>Where the Replay camera looks: a third of the way from the crease to the shooter, low over the ice.</summary>
        public static Vector3 AimPoint(Vector3 shooterEye) => new Vector3(shooterEye.x / 3f, 0.4f, -1f + (shooterEye.z + 1f) / 3f);

        void SetShowing(bool showing)
        {
            if (showing == _showing) return;
            _showing = showing;
            ReplayCamera.enabled = showing;
            _boards.ShowPicture(showing ? Picture : null);
        }

        void OnDestroy()
        {
            if (Picture == null) return;
            Picture.Release();
            SceneObjects.DestroySafely(Picture);
        }
    }
}
