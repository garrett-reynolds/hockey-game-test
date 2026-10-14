using System;
using System.Collections.Generic;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// A Team Skin: one asset that holds a team's look and sound. Team colours (rail, kick plate, seats, Ribbon Board
    /// edges), the centre-ice logo and roof banners, the crowd's home jersey, the Video Board frame and graphics, the Score
    /// Box colours, and the goal horn and goal song. The skins live in Assets/Resources/Skins; the League Skin
    /// (league.asset) is the generic look and the first skin. The save file's teamSkin setting picks one before play.
    /// <para>
    /// Art and sounds are named, as the game finds them: art in Assets/Resources/Ads, sounds in Assets/Resources/Sounds
    /// (SoundFiles: a missing sound file plays its placeholder). A blank name is none.
    /// </para>
    /// <para>
    /// Fixed parts are not here, so no skin can change them: the ice lines, the brands, the rink size and shape, the
    /// goalie, the Goal Show timing and the Calm Area colours. A skin colour that shows in the Calm Area view (rail, kick
    /// plate, seats, the near fans' team tops, the Video Board's back, words and frame) and is too near a reserved
    /// colour (ReservedColours) is replaced by the League Skin's when the skin loads (Guard).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hockey Game/Team Skin", fileName = "team-skin")]
    public sealed class TeamSkin : ScriptableObject
    {
        /// <summary>Folder under Assets/Resources that holds the skins.</summary>
        public const string Folder = "Skins";

        [Header("Team colours")]
        public Color Rail;
        public Color KickPlate;
        /// <summary>The seats (the stands' risers, under the 3D fans).</summary>
        public Color Seats;
        /// <summary>Off: the Ribbon Boards have no edges (the League Skin).</summary>
        public bool RibbonEdgesOn;
        public Color RibbonEdge;

        [Header("Centre-ice logo and roof banners (art in Assets/Resources/Ads)")]
        public string CentreIceLogo;
        /// <summary>Banners hung round the near end beside the Video Board (RoofBanners), left to right; the League Skin has its award and retired jersey banners (BannerArt).</summary>
        public string[] RoofBanners = new string[0];

        [Header("Crowd")]
        /// <summary>The home jersey the team-top fans wear (CrowdLooks.HomeShare of them).</summary>
        public Color32 HomeJersey;

        [Header("Video Board and Score Box")]
        /// <summary>The screens' background behind the words (not the brand ad slides: brands are fixed).</summary>
        public Color VideoBoardBack;
        public Color VideoBoardWords;
        public Color VideoBoardFrame;
        /// <summary>The frame's colour in the new-best light show.</summary>
        public Color VideoBoardFrameLit;
        public Color ScoreBoxBack;
        public Color ScoreBoxText;

        [Header("Sound (files in Assets/Resources/Sounds)")]
        public string GoalHorn;
        /// <summary>Plays with the horn on a Goal; blank: none (the League Skin).</summary>
        public string GoalSong;

        /// <summary>The home jersey as the crowd's colours are kept.</summary>
        public ArenaColour HomeJerseyColour => new ArenaColour(HomeJersey.r, HomeJersey.g, HomeJersey.b);

        /// <summary>The League Skin (asset name SaveData.LeagueSkin): the generic look, kept as it is.</summary>
        public static TeamSkin League
        {
            get
            {
                var league = Resources.Load<TeamSkin>(Folder + "/" + SaveData.LeagueSkin);
                if (league == null)
                    throw new InvalidOperationException("The League Skin is missing: Assets/Resources/" + Folder + "/" + SaveData.LeagueSkin + ".asset");
                return league;
            }
        }

        /// <summary>
        /// The skin with this asset name, guarded (Guard). Blank: the League Skin. No skin by that name: the League Skin,
        /// with a warning.
        /// </summary>
        public static TeamSkin Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return League;
            var skin = Resources.Load<TeamSkin>(Folder + "/" + name);
            if (skin != null) return Guard(skin);
            Debug.LogWarning("No Team Skin \"" + name + "\" in Assets/Resources/" + Folder + ": using the League Skin.");
            return League;
        }

        /// <summary>
        /// The Calm Area rules for a skin: each colour that shows in the Calm Area view and is too near a reserved colour
        /// (ReservedColours.IsSafe) is replaced by the League Skin's for that part, with one warning. Returns skin itself
        /// when nothing needs replacing (always for the League Skin: today's accepted look), or else a fixed copy (the
        /// asset is never changed). null: the League Skin.
        /// </summary>
        public static TeamSkin Guard(TeamSkin skin)
        {
            TeamSkin league = League;
            if (skin == null || skin == league) return league;
            var replaced = new List<string>();
            TeamSkin safe = null;
            void Check(string part, Color colour, Color leagueColour, Action<TeamSkin> useLeague)
            {
                if (colour == leagueColour || ReservedColours.IsSafe(ArenaColourOf(colour))) return;
                replaced.Add(part);
                if (safe == null) safe = Instantiate(skin);
                useLeague(safe);
            }
            Check(nameof(Rail), skin.Rail, league.Rail, s => s.Rail = league.Rail);
            Check(nameof(KickPlate), skin.KickPlate, league.KickPlate, s => s.KickPlate = league.KickPlate);
            Check(nameof(Seats), skin.Seats, league.Seats, s => s.Seats = league.Seats);
            Check(nameof(HomeJersey), skin.HomeJersey, league.HomeJersey, s => s.HomeJersey = league.HomeJersey);
            Check(nameof(VideoBoardBack), skin.VideoBoardBack, league.VideoBoardBack, s => s.VideoBoardBack = league.VideoBoardBack);
            Check(nameof(VideoBoardWords), skin.VideoBoardWords, league.VideoBoardWords, s => s.VideoBoardWords = league.VideoBoardWords);
            Check(nameof(VideoBoardFrame), skin.VideoBoardFrame, league.VideoBoardFrame, s => s.VideoBoardFrame = league.VideoBoardFrame);
            if (safe == null) return skin;
            safe.name = skin.name;
            Debug.LogWarning("Team Skin \"" + skin.name + "\" uses a reserved colour (Calm Area rules) for " +
                             string.Join(", ", replaced) + ": the League Skin's colours are used there instead.");
            return safe;
        }

        static ArenaColour ArenaColourOf(Color colour)
        {
            Color32 c = colour;
            return new ArenaColour(c.r, c.g, c.b);
        }

        /// <summary>skin, or the League Skin for null.</summary>
        public static TeamSkin OrLeague(TeamSkin skin) => skin != null ? skin : League;
    }
}
