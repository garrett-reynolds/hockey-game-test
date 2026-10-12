using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// A Team Skin: one asset that holds a team's look and sound. Team colours (rail, kick plate, seats, Ribbon Board
    /// edges), the centre-ice logo and roof banners, the crowd's home jersey, the Video Board frame and graphics, the Score
    /// Box colours, and the goal horn, goal song and Clap Chant. The skins live in Assets/Resources/Skins; the League Skin
    /// (league.asset) is the generic look and the first skin. The save file's teamSkin setting picks one before play.
    /// <para>
    /// Art and sounds are named, as the game finds them: art in Assets/Resources/Ads, sounds in Assets/Resources/Sounds
    /// (SoundFiles: a missing sound file plays its placeholder). A blank name is none.
    /// </para>
    /// <para>
    /// Fixed parts are not here, so no skin can change them: the ice lines, the brands, the rink size and shape, the
    /// goalie, the Goal Show timing and the Calm Area colours.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hockey Game/Team Skin", fileName = "team-skin")]
    public sealed class TeamSkin : ScriptableObject
    {
        /// <summary>Folder under Assets/Resources that holds the skins.</summary>
        public const string Folder = "Skins";
        /// <summary>The League Skin's asset name: the generic look.</summary>
        public const string LeagueName = "league";

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
        /// <summary>Banners hung from the roof over the far end, left to right; none in the League Skin.</summary>
        public string[] RoofBanners = new string[0];

        [Header("Crowd")]
        /// <summary>The home jersey (and home caps) the fans wear (CrowdLooks.HomeShare of them).</summary>
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
        /// <summary>The Clap Chant.</summary>
        public string Chant;

        /// <summary>The home jersey as the crowd's colours are kept.</summary>
        public ArenaColour HomeJerseyColour => new ArenaColour(HomeJersey.r, HomeJersey.g, HomeJersey.b);

        /// <summary>The League Skin.</summary>
        public static TeamSkin League => Load(LeagueName);

        /// <summary>The skin with this asset name; the League Skin when there is none by that name.</summary>
        public static TeamSkin Load(string name)
        {
            TeamSkin skin = string.IsNullOrEmpty(name) ? null : Resources.Load<TeamSkin>(Folder + "/" + name);
            if (skin != null) return skin;
            if (name != LeagueName)
            {
                Debug.LogWarning("No Team Skin \"" + name + "\" in Assets/Resources/" + Folder + ": using the League Skin.");
                return League;
            }
            throw new System.InvalidOperationException("The League Skin is missing: Assets/Resources/" + Folder + "/" + LeagueName + ".asset");
        }

        /// <summary>skin, or the League Skin for null.</summary>
        public static TeamSkin OrLeague(TeamSkin skin) => skin != null ? skin : League;
    }
}
