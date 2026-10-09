using HockeyGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HockeyGame
{
    /// <summary>
    /// The operator settings screen in the scene: arrow and Enter keys drive a SettingsMenu, and OnGUI draws it.
    /// Made fresh each time it opens, so the serial port list is current.
    /// </summary>
    public sealed class SettingsScreen
    {
        readonly SettingsMenu _menu;

        public SettingsScreen(SaveData save)
        {
            _menu = new SettingsMenu(save, PortNames());
        }

        /// <summary>Up/Down choose a row, Left/Right change it, Enter flips or resets. True when a setting changed (save it).</summary>
        public bool UpdateKeys(Keyboard keyboard)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame) _menu.MoveSelection(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) _menu.MoveSelection(1);
            else if (keyboard.leftArrowKey.wasPressedThisFrame) return _menu.Change(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) return _menu.Change(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                return _menu.Activate();
            return false;
        }

        /// <summary>Call from OnGUI. Draws over the whole screen.</summary>
        public void Draw(float w, float h, GUIStyle big, GUIStyle small)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(0, 30, w, 60), "SETTINGS", big);
            const float rowHeight = 32f;
            float top = 110f, labelX = w * 0.2f, valueX = w * 0.6f;
            for (int i = 0; i < _menu.RowCount; i++)
            {
                float y = top + i * rowHeight;
                bool selected = i == _menu.Selected;
                if (selected)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.15f);
                    GUI.DrawTexture(new Rect(labelX - 10, y, w * 0.6f + 20, rowHeight), Texture2D.whiteTexture);
                    GUI.color = old;
                }
                GUI.Label(new Rect(labelX, y + 4, valueX - labelX, rowHeight), (selected ? "> " : "  ") + _menu.Label(i), small);
                GUI.Label(new Rect(valueX, y + 4, w * 0.2f, rowHeight), selected ? "< " + _menu.Value(i) + " >" : _menu.Value(i), small);
            }
            GUI.Label(new Rect(0, top + _menu.RowCount * rowHeight + 20, w, 30),
                "Up/Down: choose   Left/Right: change   Enter: on/off or reset   F2: close   (saved at once)", small);
        }

        /// <summary>Serial ports on this PC now, sorted. Empty if they cannot be listed.</summary>
        static string[] PortNames()
        {
            if (!UsbPorts.TryList(out var names))
                Debug.LogWarning("Could not list serial ports.");
            return names;
        }
    }
}
