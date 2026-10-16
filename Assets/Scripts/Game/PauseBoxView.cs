using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Draws the PAUSED box (#196, pick B) in look C "TV graphic": the Round dimmed behind, a centred navy box with a gold stripe on top,
    /// a red PAUSED header, and two buttons (Resume, Quit to start). The chosen button is gold with navy words, the other blue with white.
    /// Where things sit is PauseLayout. This only draws.
    /// </summary>
    public sealed class PauseBoxView
    {
        public const string Title = "PAUSED";
        public static readonly string[] ButtonWords = { "Resume", "Quit to start" };
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);

        GUIStyle _style;

        public void Draw(float w, float h, int selected)
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
            float s = PauseLayout.Scale(w, h);
            Fill(new Rect(0f, 0f, w, h), Dim);
            Fill(PauseLayout.Box(w, h), LookC.Navy);
            Fill(PauseLayout.Stripe(w, h), LookC.Gold);
            Rect header = PauseLayout.Header(w, h);
            Fill(header, LookC.Red);
            Word(header, Title, Color.white, (int)(44 * s));
            for (int i = 0; i < ButtonWords.Length; i++)
            {
                Rect button = PauseLayout.Button(w, h, i);
                bool on = i == selected;
                Fill(button, on ? LookC.Gold : LookC.Blue);
                Word(button, ButtonWords[i], on ? (Color)LookC.Navy : Color.white, (int)(28 * s));
            }
        }

        static void Fill(Rect r, Color c)
        {
            Color was = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = was;
        }

        void Word(Rect r, string text, Color colour, int size)
        {
            _style.fontSize = Mathf.Max(10, size);
            _style.normal.textColor = colour;
            GUI.Label(r, text, _style);
        }
    }
}
