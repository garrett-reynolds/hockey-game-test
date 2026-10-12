using System;

namespace HockeyGame.Core
{
    /// <summary>One fan's own look: where in its clip it starts (Phase, 0..1 of a loop) and its shirt, skin and hair.</summary>
    public readonly struct FanLook
    {
        public readonly double Phase;
        public readonly ArenaColour Shirt, Skin, Hair;

        public FanLook(double phase, ArenaColour shirt, ArenaColour skin, ArenaColour hair)
        {
            Phase = phase; Shirt = shirt; Skin = skin; Hair = hair;
        }
    }

    /// <summary>
    /// How the 3D crowd looks: what the fans in a zone do for each Arena Show crowd state, and each fan's random look.
    /// The first test (#94) has one clip, sit-idle: every moving state plays it until the other clips come (#95).
    /// </summary>
    public static class CrowdLooks
    {
        /// <summary>The share of fans in the home jersey; the rest wear dark plain clothes.</summary>
        public const double HomeShare = 0.4;

        /// <summary>The home jersey: a placeholder burgundy until the Team Skin holds it. Not a reserved colour (the goalie's
        /// blue, white and grey gear, the Easy mode green glow, puck black or the red posts).</summary>
        public static readonly ArenaColour HomeJersey = new ArenaColour(112, 30, 46);

        /// <summary>Dark plain clothes: charcoal, brown, olive, plum and slate (no black, blue or red).</summary>
        public static readonly ArenaColour[] DarkPlain =
        {
            new ArenaColour(50, 50, 54), new ArenaColour(62, 46, 36), new ArenaColour(52, 56, 40),
            new ArenaColour(56, 40, 56), new ArenaColour(40, 46, 54)
        };

        public static readonly ArenaColour[] Skins =
        {
            new ArenaColour(232, 190, 160), new ArenaColour(205, 150, 115), new ArenaColour(160, 110, 80),
            new ArenaColour(110, 75, 55), new ArenaColour(75, 52, 40)
        };

        public static readonly ArenaColour[] Hairs =
        {
            new ArenaColour(30, 24, 20), new ArenaColour(70, 50, 35), new ArenaColour(120, 90, 60),
            new ArenaColour(170, 140, 95), new ArenaColour(130, 128, 125)
        };

        /// <summary>How fast a zone's clip clock runs in this state (1 = the clip's own speed). Still stops the clock, so
        /// the fans hold the pose they are in (no jump back to a rest pose).</summary>
        public static double Motion(CrowdState state) => state == CrowdState.Still ? 0.0 : 1.0;

        /// <summary>How bright the zone's fans are in this state (1 = as lit). Still is dim (the Calm Area rules). It
        /// depends on the state alone, so the picture test sees the same dim as play.</summary>
        public static double Light(CrowdState state) => state == CrowdState.Still ? StillLight : 1.0;

        /// <summary>The Calm Area's still fans' brightness.</summary>
        public const double StillLight = 0.7;

        /// <summary>count fans' random looks.</summary>
        public static FanLook[] Make(int count, Random random)
        {
            var looks = new FanLook[count];
            for (int i = 0; i < count; i++)
            {
                double phase = random.NextDouble();
                ArenaColour shirt = random.NextDouble() < HomeShare ? HomeJersey : DarkPlain[random.Next(DarkPlain.Length)];
                looks[i] = new FanLook(phase, shirt, Skins[random.Next(Skins.Length)], Hairs[random.Next(Hairs.Length)]);
            }
            return looks;
        }
    }
}
