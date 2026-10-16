using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HockeyGame.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The PlayMode check of Pause (#196): in the real Rink scene a puck in flight and the goalie do not move while the Round is paused,
    /// and go on from the same place after Resume. Enters Play, so it runs only in the full run. When the folder above the project has a
    /// _notes folder it also saves two pictures there (the open PAUSED box, and the frame after Resume) and a log of the puck positions.
    /// </summary>
    public class PauseInPlayTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static Vector3 GoalieSignature(Transform root)
        {
            Vector3 sum = Vector3.zero;
            foreach (var t in root.GetComponentsInChildren<Transform>())
                sum += t.position + t.rotation.eulerAngles * 0.01f;
            return sum;
        }

        static string Fmt(Vector3 v) => v.x.ToString("F4", CultureInfo.InvariantCulture) + "," + v.y.ToString("F4", CultureInfo.InvariantCulture)
            + "," + v.z.ToString("F4", CultureInfo.InvariantCulture);

        // EditMode can advance this coroutine before the next game Update (#217). Wait for the
        // real driver's public response, with a bound; the caller still asserts pause or resume.
        static IEnumerator PressEsc(Keyboard keyboard, ScreenFlow flow)
        {
            bool wasPaused = flow.Paused;
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                while (flow.Paused == wasPaused && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            while (keyboard.escapeKey.isPressed && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.IsFalse(keyboard.escapeKey.isPressed, "the synthetic Esc key is released");
        }

        [UnityTest, Category("Slow"), Explicit("Slow: enters Play (runs only in the full run)")]
        public IEnumerator A_puck_in_flight_and_the_goalie_stand_still_while_paused_and_go_on_after_Resume()
        {
            bool oldMute = EditorUtility.audioMasterMute;
            bool oldBackground = Application.runInBackground;
            var hidden = new System.Collections.Generic.List<Crowd>();
            var log = new StringBuilder();
            bool finished = false;
            Keyboard keyboard = null;
            InputSettings.BackgroundBehavior? oldBackgroundBehavior = null;
            InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior = default;
            string notes = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_notes"));
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Rink.unity");
                yield return new EnterPlayMode(false);
                Application.runInBackground = true;
                EditorUtility.audioMasterMute = true;
                foreach (var c in Object.FindObjectsByType<Crowd>(FindObjectsSortMode.None))
                    if (c.Drawn) { c.Drawn = false; hidden.Add(c); }

                RoundDriver driver = null;
                for (int i = 0; i < 300 && driver == null; i++)
                {
                    driver = Object.FindFirstObjectByType<RoundDriver>();
                    yield return null;
                }
                Assert.IsNotNull(driver, "the Rink scene has a RoundDriver");
                var flow = (ScreenFlow)typeof(RoundDriver).GetField("_flow", Any).GetValue(driver);
                var effects = (PhysicsShotEffects)typeof(RoundDriver).GetField("_effects", Any).GetValue(driver);
                var goalie = (GoalieView)typeof(RoundDriver).GetField("_goalie", Any).GetValue(driver);
                // Esc goes through the real path (RoundDriver.Update and UpdatePaused read Keyboard.current); the key comes from a fake
                // keyboard device made in code. No OS input is sent.
                oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                // Set focus policy before creating the device: an unfocused Editor can disable it (#217).
                keyboard = InputSystem.AddDevice<Keyboard>("PauseTestKeyboard");
                InputSystem.EnableDevice(keyboard);
                yield return null;

                flow.Press();
                for (int i = 0; i < 1500 && flow.Round.Phase != RoundPhase.ShootingWindow; i++) yield return null;
                Assert.AreEqual(RoundPhase.ShootingWindow, flow.Round.Phase, "the Round reaches the shooting window");
                flow.SubmitShot(50, 50, 100, false);
                for (int i = 0; i < 600 && !(effects.Shot != null && effects.Shot.Launched && effects.Shot.SecondsSinceShot > 0.08); i++) yield return null;
                Assert.IsTrue(effects.Shot != null && effects.Shot.Launched, "the puck is in flight");
                Rigidbody puck = effects.Shot.Puck;

                Vector3 pausedAt = puck.position;
                log.AppendLine("puck at pause request " + Fmt(pausedAt) + " speed " + puck.linearVelocity.magnitude.ToString("F2", CultureInfo.InvariantCulture));
                yield return PressEsc(keyboard, flow);
                Assert.IsTrue(flow.Paused, "Esc in a Round pauses it");
                Assert.IsTrue(PauseSwitch.IsPaused);
                Assert.AreEqual(0f, Time.timeScale, "game time stopped");
                Assert.IsTrue(AudioListener.pause, "sound paused");
                yield return null;
                yield return null;

                Vector3 puckBefore = puck.position;
                Vector3 goalieBefore = GoalieSignature(goalie.Body);
                double elapsedBefore = flow.Round.PhaseSecondsElapsed;
                double clockBefore = PauseSwitch.Seconds;
                double secondsBefore = effects.Shot.SecondsSinceShot;
                log.AppendLine("puck paused (baseline) " + Fmt(puckBefore));
                yield return new WaitForSecondsRealtime(2f);
                yield return new WaitForEndOfFrame();
                Vector3 puckAfter = puck.position;
                log.AppendLine("puck after 2 s paused " + Fmt(puckAfter));
                if (Directory.Exists(notes))
                {
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(notes, "t196-paused.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                Assert.AreEqual(puckBefore, puckAfter, "the puck does not move while paused");
                Assert.AreEqual(goalieBefore, GoalieSignature(goalie.Body), "the goalie does not move while paused");
                Assert.AreEqual(elapsedBefore, flow.Round.PhaseSecondsElapsed, 1e-9, "the Round clock stands still");
                Assert.AreEqual(clockBefore, PauseSwitch.Seconds, 1e-6, "the clock of the ribbons, boards and crowd stands still");
                Assert.AreEqual(secondsBefore, effects.Shot.SecondsSinceShot, 1e-9, "the puck's flight clock stands still");

                float speedAtPause = puck.linearVelocity.magnitude; // a paused body keeps its velocity (the puck is only frozen by time)
                float gameTimeAtResume = Time.time;
                yield return PressEsc(keyboard, flow);
                Assert.IsFalse(flow.Paused, "Esc in the PAUSED box resumes");
                Assert.IsFalse(PauseSwitch.IsPaused);
                Assert.IsFalse(AudioListener.pause);
                Assert.Less(PauseSwitch.Seconds - clockBefore, 0.5, "after Resume the clock carries on from the same moment, not 2 s later");
                yield return null;
                yield return new WaitForEndOfFrame();
                Vector3 puckResumed = puck.position;
                log.AppendLine("puck after Resume (1 frame) " + Fmt(puckResumed));
                if (Directory.Exists(notes))
                {
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(notes, "t196-resumed.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                // It goes on from the same place: it moved no further than its speed allows in the game time that passed since Resume
                // (plus one physics step), and far less than the 2 s of real time it stood paused.
                float gameSeconds = Time.time - gameTimeAtResume;
                float allowed = speedAtPause * 1.2f * (gameSeconds + Time.fixedDeltaTime) + 0.01f;
                log.AppendLine("game seconds since Resume " + gameSeconds.ToString("F4", CultureInfo.InvariantCulture) + ", speed " + speedAtPause.ToString("F2", CultureInfo.InvariantCulture)
                    + ", moved " + (puckResumed - puckAfter).magnitude.ToString("F3", CultureInfo.InvariantCulture) + ", allowed " + allowed.ToString("F3", CultureInfo.InvariantCulture)
                    + ", unscaled delta " + Time.unscaledDeltaTime.ToString("F4", CultureInfo.InvariantCulture));
                Assert.LessOrEqual((puckResumed - puckAfter).magnitude, allowed, "it goes on from the same place, not from where it would be after 2 s");
                for (int i = 0; i < 10; i++) yield return null;
                Assert.AreNotEqual(puckAfter, puck.position, "the puck moves again after Resume");
                finished = true;
            }
            finally
            {
                if (!finished) EditorApplication.isPlaying = false; // a failed check must not leave the Editor in Play
                if (Directory.Exists(notes)) File.WriteAllText(Path.Combine(notes, "t196-puck.log"), log.ToString());
                Debug.Log("[PauseInPlay] " + log);
                PauseSwitch.Set(false);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (oldBackgroundBehavior.HasValue)
                {
                    InputSystem.settings.backgroundBehavior = oldBackgroundBehavior.Value;
                    InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
                }
                foreach (var c in hidden) if (c != null) c.Drawn = true;
                EditorUtility.audioMasterMute = oldMute;
                Application.runInBackground = oldBackground;
            }
            yield return new ExitPlayMode();
        }
    }
}
