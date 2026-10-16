using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HockeyGame
{
    /// <summary>
    /// The Replay (#91): it records what is seen of each Shot from the Shot on (ReplayTrack), keeps it if a Replay follows
    /// the Whistle's result (a Goal, a Save or a Frame Hit; ReplayTiming.Follows), and while the Arena Show says the Replay plays it shows that Shot again in slow motion (ReplayTiming) to a second camera behind the
    /// near net looking back at the shooter. That camera draws a small render texture that both boards show. The camera
    /// draws only while the Replay plays. The recorded poses go back to the live ones at the start of the next frame
    /// (RestoreLive, from RoundDriver) so the game's own views carry on from where they were. Runs after the views'
    /// LateUpdate (BreakawayView), so it records and shows what they set.
    /// <para>
    /// The physics puck (ADR 0004) is not in the track: Unity physics moves it, and each Shot has a new one. Its Rigidbody
    /// pose is recorded onto a stand-in puck with no Rigidbody or hit shapes (ReplayPhysicsPuck), which the Replay moves
    /// and shows instead, with the live puck's renderers off while the Replay shows.
    /// </para>
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
        Func<Rigidbody> _livePuck;      // the current Shot's physics puck (null before the first); see ReplayPhysicsPuck
        GameObject _replayPuck;          // its stand-in: shown only on Replay frames
        WaterBottle _liveBottle;         // the water bottle on the net (null without ReplayBottle); see ReplayBottle
        GameObject _replayBottle;        // its stand-in: shown only on Replay frames
        readonly List<Renderer> _hiddenLive = new List<Renderer>(); // the live puck's renderers the Replay turned off
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
        /// Sets the Replay up. parts: what the Replay plays back by their transforms, none with a Rigidbody (the physics puck
        /// goes through ReplayPhysicsPuck): the goalie and anything else that is seen. shooter: the player's camera, copied for the Replay camera's look.
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

        /// <summary>
        /// Replays the physics puck too: each frame of the recording, livePuck's Rigidbody pose (position, rotation, shown or
        /// not) goes onto a stand-in puck (PhysicsPuck.MakeLook: no Rigidbody, no hit shapes), which the track records. On
        /// Replay frames the stand-in shows the recorded flight and the live puck's renderers are off (RestoreLive turns them
        /// back on). livePuck: the current Shot's puck, a new one each Shot; null before the first. findFile as ModelFiles.
        /// </summary>
        public void ReplayPhysicsPuck(Func<Rigidbody> livePuck, Func<string, GameObject> findFile = null)
        {
            _livePuck = livePuck;
            _replayPuck = PhysicsPuck.MakeLook(transform, findFile);
            _replayPuck.SetActive(false);
        }

        /// <summary>The stand-in the Replay shows for the physics puck (null without ReplayPhysicsPuck).</summary>
        public GameObject ReplayPuck => _replayPuck;

        /// <summary>
        /// Replays the water bottle too (#164): it has a Rigidbody (a knocked bottle is free physics), so as with the puck its
        /// pose goes onto a stand-in with no Rigidbody (WaterBottle.MakeLook), which the track records and the Replay shows while
        /// the live bottle's renderers are off. The live bottle is never touched, so after the Replay it is just as the game has it.
        /// </summary>
        public void ReplayBottle(WaterBottle bottle, Func<string, GameObject> findFile = null)
        {
            _liveBottle = bottle;
            _replayBottle = WaterBottle.MakeLook(transform, findFile);
            _replayBottle.SetActive(false);
        }

        /// <summary>The stand-in the Replay shows for the water bottle (null without ReplayBottle).</summary>
        public GameObject ReplayBottleLook => _replayBottle;

        /// <summary>The Arena Show's answer this frame: the Replay plays while its camera mode is Replay.</summary>
        public void Show(ArenaState show) => _show = show;

        /// <summary>Puts the live poses back after a frame that showed the Replay. Call first thing each frame.</summary>
        public void RestoreLive()
        {
            _track.Restore();
            ShowLivePuck();
        }

        void LateUpdate()
        {
            if (_round != null && !PauseSwitch.StandStill) Step(); // paused (#196): the Replay stands still
        }

        /// <summary>One frame, after the views have posed the scene: records the Shot, and plays the Replay while the Arena Show says so.</summary>
        public void Step()
        {
            // An AI clip on the goalie (PROTOTYPE, it starts at the Whistle) is recorded and replayed like any pose: RestoreLive
            // puts its live pose back at the next frame's start, before the clip poses the bones again.
            Record();
            bool play = _show.Camera == CameraMode.Replay && _track.SampleCount > 0;
            if (play)
            {
                _track.Apply(ReplayTiming.ShotSecondsAt(_show.ReplaySeconds, _round.ResultDelaySeconds, BottleKnocked));
                HideLivePuck();
            }
            SetShowing(play);
        }

        /// <summary>
        /// Records each Shot from the frame it is shot (the result is not known until the Whistle), and at the Whistle keeps
        /// the recording only if a Replay follows. Records until just after the clip ends (ReplayTiming.ClipEnd). A hold with
        /// no Shot (Too late) or with no Replay drops the last recording.
        /// </summary>
        void Record()
        {
            if (_round.Phase != RoundPhase.ResultHold)
            {
                _begun = _recording = false;
                return;
            }
            if (!_begun)
            {
                _begun = true;
                _recording = _round.LastShot.HasValue && _round.ReplayOn; // the Shot frame; Too late has no Shot; Replay off (#145, and always in Test Mode, #156): nothing to record
                if (_recording)
                {
                    _track.Begin(Parts());
                    Aim(_shooter.transform.position);
                }
                else _track.Begin(); // no Replay: drop the last one
            }
            if (!_recording) return;
            if (_round.ResultShowing && !_round.ReplayFollows)
            {
                _track.Begin(); // the Whistle gave a result with no Replay (a Wide Miss): drop the recording
                _recording = false;
                return;
            }
            double seconds = _round.PhaseSecondsElapsed;
            PoseReplayPuck();
            PoseReplayBottle();
            _track.Record(seconds);
            if (_replayPuck != null) _replayPuck.SetActive(false); // seen only on Replay frames
            if (_replayBottle != null) _replayBottle.SetActive(false);
            if (_round.ResultShowing && seconds > ReplayTiming.ClipEnd(_round.ResultDelaySeconds, BottleKnocked) + RecordMargin) _recording = false;
        }

        /// <summary>True when the water bottle was knocked off this Round (the Whistle's Goal did it): the clip then runs on until it lands (#164).</summary>
        bool BottleKnocked => _replayBottle != null && _liveBottle != null && _liveBottle.Knocked;

        /// <summary>What the track records: the parts given to Init, then the physics puck's stand-in.</summary>
        Transform[] Parts()
        {
            var parts = new List<Transform>(_parts);
            if (_replayPuck != null) parts.Add(_replayPuck.transform);
            if (_replayBottle != null) parts.Add(_replayBottle.transform);
            return parts.ToArray();
        }

        /// <summary>The bottle's stand-in takes the live bottle's pose (read only), shown as the live bottle is.</summary>
        void PoseReplayBottle()
        {
            if (_replayBottle == null) return;
            bool shown = _liveBottle != null && _liveBottle.Root != null && _liveBottle.Root.activeInHierarchy;
            if (shown) _replayBottle.transform.SetPositionAndRotation(_liveBottle.Root.transform.position, _liveBottle.Root.transform.rotation);
            _replayBottle.SetActive(shown);
        }

        /// <summary>The stand-in takes the live puck's Rigidbody pose, shown as the live puck is (read only: physics keeps the puck).</summary>
        void PoseReplayPuck()
        {
            if (_replayPuck == null) return;
            Rigidbody live = _livePuck?.Invoke();
            bool shown = live != null && live.gameObject.activeInHierarchy;
            if (shown) _replayPuck.transform.SetPositionAndRotation(live.position, live.rotation);
            _replayPuck.SetActive(shown);
        }

        /// <summary>On a Replay frame: the live puck is not drawn (the stand-in is), with no change to its physics.</summary>
        void HideLivePuck()
        {
            if (_hiddenLive.Count > 0) return;
            Rigidbody live = _livePuck?.Invoke();
            if (live != null) HideRenderers(live.gameObject);
            if (_replayBottle != null && _liveBottle != null && _liveBottle.Root != null) HideRenderers(_liveBottle.Root);
        }

        void HideRenderers(GameObject live)
        {
            foreach (Renderer r in live.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                r.enabled = false;
                _hiddenLive.Add(r);
            }
        }

        void ShowLivePuck()
        {
            foreach (Renderer r in _hiddenLive)
                if (r != null) r.enabled = true;
            _hiddenLive.Clear();
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
