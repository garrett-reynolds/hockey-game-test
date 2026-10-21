using System.IO;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The one shared SaveData for the running game: loaded once at launch from the PC, saved on change.
    /// Everything that reads or changes settings or the best score (Round flow, settings screen, USB)
    /// uses Current and calls Save() after a change, so no copy overwrites another's change.
    /// </summary>
    public static class SaveStore
    {
        static SaveData _current;

        /// <summary>%USERPROFILE%/AppData/LocalLow/&lt;company&gt;/&lt;product&gt;/save.txt on Windows.</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath, "save.txt");

        public static SaveData Current => _current ?? (_current = LoadChecked());

        // A listed custom logo with no picture file falls back to the default art, with a log line, and is dropped (#226).
        static SaveData LoadChecked()
        {
            SaveData data = SaveData.Load(FilePath);
            data.DropMissingLogos(CustomLogos.Files.Exists, Debug.LogWarning);
            return data;
        }

        public static void Save()
        {
            if (!Current.Save(FilePath))
                Debug.LogWarning("Could not save settings to " + FilePath);
            else
                WebFlush.Request(); // web build: copy to the browser's stored files (#228)
        }

        // Load fresh from disk on each launch and each Play-mode start, even with domain reload off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLaunch()
        {
            _current = null;
        }
    }
}
