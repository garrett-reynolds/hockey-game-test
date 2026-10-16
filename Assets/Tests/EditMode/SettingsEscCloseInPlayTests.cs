using System.Collections;
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
    /// The Settings bottom bar in Play (#195 review): ESC CLOSE is a button whose click acts like Esc, and a click on SAVE writes the
    /// file (UpdateInput says so). The clicks come from a fake mouse device made in code and fed to SettingsScreen.UpdateInput; no OS
    /// input is sent. Runs in an empty scene, so no game reads the fake mouse. Enters Play, so it runs only in the full run.
    /// </summary>
    public class SettingsEscCloseInPlayTests
    {
        // A click: the button down on one frame, then up on the next.
        static IEnumerator Click(SettingsScreen screen, Mouse mouse, Vector2 at, System.Action<bool> persisted)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(MouseButton.Left));
            InputSystem.Update(); // the test reads the mouse itself in this same frame, so process the event now
            persisted(screen.UpdateInput(null, mouse));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at });
            InputSystem.Update();
            yield return null;
        }

        [UnityTest, Category("Slow"), Explicit("Slow: enters Play (runs only in the full run)")]
        public IEnumerator ESC_CLOSE_closes_or_asks_Save_changes_and_SAVE_by_mouse_writes_the_file()
        {
            bool oldMute = EditorUtility.audioMasterMute;
            bool oldBackground = Application.runInBackground;
            var oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            var oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse mouse = null;
            SettingsScreen screen = null;
            bool finished = false;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                yield return new EnterPlayMode(false);
                Application.runInBackground = true;
                EditorUtility.audioMasterMute = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse = InputSystem.AddDevice<Mouse>("EscCloseTestMouse");

                screen = new SettingsScreen(new SaveData());
                screen.Open();
                for (int i = 0; i < 5; i++) yield return null;

                // Nothing unsaved: a click on ESC CLOSE closes at once.
                bool persisted = false;
                Vector2 esc = screen.EscClosePoint();
                Assert.Greater(esc.x, 0f, "ESC CLOSE has a place on screen");
                yield return Click(screen, mouse, esc, p => persisted = p);
                Assert.IsTrue(screen.TakeCloseApproved(), "nothing unsaved: the click closes at once");
                Assert.IsFalse(persisted);

                // Something unsaved: the click asks "Save changes?" first.
                screen.Open();
                for (int i = 0; i < 3; i++) yield return null;
                screen.Menu.Select(0);
                screen.Menu.Change(-1); // Master volume down: unsaved
                yield return Click(screen, mouse, screen.EscClosePoint(), p => persisted = p);
                Assert.IsFalse(screen.TakeCloseApproved(), "something is unsaved: not closed yet");
                Assert.IsTrue(screen.ConfirmOpen, "the Save changes? box asks");

                // A click on SAVE with the mouse says the file must be written.
                screen.Open();
                for (int i = 0; i < 3; i++) yield return null;
                screen.Menu.Select(0);
                screen.Menu.Change(-1);
                yield return Click(screen, mouse, screen.SavePoint(), p => persisted = p);
                Assert.IsTrue(persisted, "a mouse click on SAVE asks the caller to write the file");
                Assert.IsFalse(screen.Menu.HasUnsavedChanges);
                finished = true;
            }
            finally
            {
                if (!finished) EditorApplication.isPlaying = false;
                if (screen != null) screen.Close();
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
                EditorUtility.audioMasterMute = oldMute;
                Application.runInBackground = oldBackground;
            }
            yield return new ExitPlayMode();
        }
    }
}
