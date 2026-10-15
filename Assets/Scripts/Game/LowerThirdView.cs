using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Draws the lower third (#141, look A): a thin full-width TV bar at the very bottom with a gold stripe on top, a red SHOOTOUT tab,
    /// the tally, the gold stat and a blue SHOT n OF n cap. It is on screen the whole Round; what it says and when (LowerThird) is decided
    /// in the core, and where each part sits is LowerThirdLayout (a test checks it at several screen sizes). This only draws.
    /// <para>
    /// The colours are look C's (LookC, one list). Each line's GUIContent and fitted font size are made only when its text or its box's
    /// width changes (once per Shot), not on every OnGUI call (#151).
    /// </para>
    /// </summary>
    public sealed class LowerThirdView
    {
        public const string TabText = "SHOOTOUT";

        GUIStyle _white, _gold;
        readonly Words _tab = new Words(), _tally = new Words(), _stat = new Words(), _shotOf = new Words();

        // One line of words: its GUIContent, and the font size that fits (kept while the text and the box's width stay the same).
        sealed class Words
        {
            public string Text;
            public GUIContent Content;
            public float FittedWidth = -1f;
            public int Size;
        }

        /// <summary>Draws the bar with these words on a screen of w by h pixels.</summary>
        public void Draw(float w, float h, LowerThirdText text)
        {
            if (_white == null) Make();
            float s = LowerThirdLayout.Scale(w, h);
            Rect bar = LowerThirdLayout.Bar(w, h), tab = LowerThirdLayout.Tab(w, h), tally = LowerThirdLayout.Tally(w, h);
            Rect stat = LowerThirdLayout.Stat(w, h), cap = LowerThirdLayout.Cap(w, h);

            Fill(LowerThirdLayout.Stripe(w, h), LookC.Gold);
            Fill(bar, LookC.Navy);
            Fill(tab, LookC.Red);
            Fill(new Rect(tab.xMax, bar.y, tally.x - tab.xMax, bar.height), LookC.Gold); // the gold edge after the tab
            Fill(new Rect(stat.x, bar.y + 8f * s, 2f * s, bar.height - 16f * s), LookC.Blue); // the divider before the stat
            Fill(cap, LookC.Blue);

            float pad = 20f * s;
            Label(tab, TabText, _white, _tab, (int)(20 * s), TextAnchor.MiddleCenter, 0f);
            Label(tally, text.Tally, _white, _tally, (int)(22 * s), TextAnchor.MiddleLeft, pad);
            Label(stat, text.Stat, _gold, _stat, (int)(21 * s), TextAnchor.MiddleLeft, pad);
            Label(cap, text.ShotOf, _white, _shotOf, (int)(22 * s), TextAnchor.MiddleCenter, 0f);
        }

        static void Fill(Rect r, Color c)
        {
            Color was = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = was;
        }

        // The words at the biggest size up to maxSize that fits the box (less pad on each side). The content and the size are kept while
        // the text and the width stay the same, so a frame only draws.
        static void Label(Rect box, string text, GUIStyle style, Words words, int maxSize, TextAnchor anchor, float pad)
        {
            if (string.IsNullOrEmpty(text)) return;
            Rect inner = new Rect(box.x + pad, box.y, box.width - 2f * pad, box.height);
            if (!ReferenceEquals(words.Text, text) || words.FittedWidth != inner.width)
            {
                words.Text = text;
                words.Content = new GUIContent(text);
                words.FittedWidth = inner.width;
                int size = maxSize;
                for (; size > 10; size--)
                {
                    style.fontSize = size;
                    if (style.CalcSize(words.Content).x <= inner.width) break;
                }
                words.Size = size;
            }
            style.fontSize = words.Size;
            style.alignment = anchor;
            GUI.Label(inner, words.Content, style);
        }

        void Make()
        {
            _white = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, wordWrap = false, clipping = TextClipping.Clip };
            _white.normal.textColor = Color.white;
            _gold = new GUIStyle(_white);
            _gold.normal.textColor = LookC.Gold;
        }
    }
}
