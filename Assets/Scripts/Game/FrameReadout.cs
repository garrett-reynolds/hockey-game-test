using System.Globalization;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The dev-only frame readout (#85): the GPU and CPU (main thread) milliseconds of a recent frame, from the
    /// FrameTimingManager, in a screen corner. Only in the Editor and development builds (Shows); a normal build has none.
    /// Times the platform does not measure (often the GPU in the Editor) show as n/a.
    /// </summary>
    public sealed class FrameReadout : MonoBehaviour
    {
        readonly FrameTiming[] _timing = new FrameTiming[1];
        string _text = Text(double.NaN, double.NaN);
        GUIStyle _style;

        /// <summary>True when the readout shows: in the Editor and development builds (Debug.isDebugBuild), never in a normal build.</summary>
        public static bool Shows(bool debugBuild) => debugBuild;

        /// <summary>Adds the readout to target when it shows in this build.</summary>
        public static void AddTo(GameObject target)
        {
            if (Shows(Debug.isDebugBuild)) target.AddComponent<FrameReadout>();
        }

        /// <summary>The readout's line: each time in ms to one decimal, or n/a when not measured (0, negative or NaN).</summary>
        public static string Text(double cpuMs, double gpuMs) => "CPU " + Ms(cpuMs) + "   GPU " + Ms(gpuMs);

        static string Ms(double ms) => ms > 0 ? ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : "n/a";

        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                _text = Text(_timing[0].cpuMainThreadFrameTime, _timing[0].gpuFrameTime);
        }

        void OnGUI()
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperRight, normal = { textColor = Color.white } };
            GUI.Label(new Rect(Screen.width - 330, 10, 320, 30), _text, _style);
        }
    }
}
