using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>One of the crowd's body types: which of the 12 outfits (Base, 0..11; Tools/make_crowd.py OUTFITS, the row of
    /// Crowd.BodyFiles), which of its baked shapes (Shape: 0 regular; Tools/make_crowd.py BODY_TYPES) and its height range
    /// (1 = as made). The teens wear the same outfit files, shorter.</summary>
    public readonly struct BodyType
    {
        public readonly string Name;
        public readonly int Base, Shape;
        public readonly double MinHeight, MaxHeight;

        public BodyType(string name, int @base, int shape, double minHeight, double maxHeight)
        {
            Name = name; Base = @base; Shape = shape; MinHeight = minHeight; MaxHeight = maxHeight;
        }
    }

    /// <summary>One fan's own look: where in its clips it is (Phase, 0..1 of a loop), its body type (an index into
    /// CrowdLooks.BodyTypes) and height, its skin and hair colours, whether it wears a team top (then Shirt is the home
    /// jersey; otherwise the outfit's own colours show and Shirt is default) and whether it is one of the fans who wave in
    /// the skate-in.</summary>
    public readonly struct FanLook
    {
        public readonly double Phase, Height;
        public readonly int Body;
        public readonly ArenaColour Shirt, Skin, Hair;
        public readonly bool TeamTop, Waves;
        /// <summary>What the fan does on a Goal (a CrowdLooks.GoalAct).</summary>
        public readonly int GoalAct;
        /// <summary>Whether the fan's "oh!" tips the head back.</summary>
        public readonly bool OhBack;
        /// <summary>Whether the fan claps while its zone claps (the start screen, Round over); otherwise it sits still. Fixed per fan.</summary>
        public readonly bool Claps;

        public FanLook(double phase, int body, double height, bool teamTop, ArenaColour shirt, ArenaColour skin, ArenaColour hair, bool waves,
                       int goalAct = CrowdLooks.GoalAct.FistPumpLeft, bool ohBack = false, bool claps = true)
        {
            Phase = phase; Body = body; Height = height; TeamTop = teamTop; Shirt = shirt; Skin = skin; Hair = hair; Waves = waves;
            GoalAct = goalAct; OhBack = ohBack; Claps = claps;
        }
    }

    /// <summary>
    /// How the 3D crowd looks: what the fans in a zone do for each Arena Show crowd state, and each fan's random look.
    /// Neighbouring fans never wear the same outfit, so no two neighbours look alike.
    /// </summary>
    public static class CrowdLooks
    {
        /// <summary>The share of fans who wear a team top in the home jersey; the rest wear their outfit's own colours.</summary>
        public const double HomeShare = 0.33;
        /// <summary>The share of fans who wave in the skate-in; the rest sit and make small moves.</summary>
        public const double WaveShare = 0.3;

        /// <summary>What a fan does while its zone cheers a Goal: most stand and pump a fist (left or right), some stay seated.</summary>
        public static class GoalAct
        {
            public const int FistPumpLeft = 0, FistPumpRight = 1, SeatedClap = 2, SeatedStill = 3;
        }
        /// <summary>The share of fans who stay seated on a Goal (half clap, half sit still). A starting value to tune.</summary>
        public const double SeatedOnGoalShare = 0.2;
        /// <summary>The share of fans whose "oh!" tips the head back (HitRecieve_2) rather than the small lean back.</summary>
        public const double OhBackShare = 0.3;
        /// <summary>The share of fans who clap while their zone claps (the start screen, Round over); the rest sit still. A starting value to tune.</summary>
        public const double ClapShare = 0.6;

        /// <summary>The 17 body types: the 12 Modular outfits (Crowd.BodyFiles' rows: 0 man in a hoodie, 1 jacket, 2 suit,
        /// 3 worker, 4 punk, 5 farmer, 6 woman in a top, 7 suit, 8 dress, 9 worker, 10 punk, 11 adventurer) and five teens,
        /// who wear four of the same outfits, shorter.</summary>
        public static readonly BodyType[] BodyTypes =
        {
            new BodyType("man in a hoodie", 0, 0, 0.97, 1.03), new BodyType("man in a jacket", 1, 0, 0.97, 1.03),
            new BodyType("man in a suit", 2, 0, 0.97, 1.03), new BodyType("worker", 3, 0, 0.97, 1.03),
            new BodyType("punk", 4, 0, 0.97, 1.03), new BodyType("farmer", 5, 0, 0.97, 1.03),
            new BodyType("woman in a top", 6, 0, 0.95, 1.01), new BodyType("woman in a suit", 7, 0, 0.95, 1.01),
            new BodyType("woman in a dress", 8, 0, 0.95, 1.01), new BodyType("woman worker", 9, 0, 0.95, 1.01),
            new BodyType("woman punk", 10, 0, 0.95, 1.01), new BodyType("woman adventurer", 11, 0, 0.95, 1.01),
            new BodyType("teen boy in a hoodie", 0, 0, 0.86, 0.92), new BodyType("teen boy punk", 4, 0, 0.86, 0.92),
            new BodyType("teen girl in a top", 6, 0, 0.85, 0.91), new BodyType("teen girl punk", 10, 0, 0.85, 0.91),
            new BodyType("teen boy in a jacket", 1, 0, 0.86, 0.92),
        };

        /// <summary>How many outfits (body files per level) there are.</summary>
        public const int Outfits = 12;

        /// <summary>An outfit's own colour (from a baked palette) moved off any reserved colour: kept when it is safe
        /// (ReservedColours.IsSafe), otherwise stepped toward the flat tone in 10 % steps until it is. The flat tone is safe
        /// already, so this always ends.</summary>
        public static ArenaColour SafeOutfitColour(ArenaColour c)
        {
            ArenaColour moved = c;
            for (int step = 1; step <= 10 && !ReservedColours.IsSafe(moved); step++)
                moved = Toward(c, FlatTone, step / 10.0);
            return moved;
        }

        static ArenaColour Toward(ArenaColour from, ArenaColour to, double t) => new ArenaColour(
            (byte)Math.Round(from.R + (to.R - from.R) * t), (byte)Math.Round(from.G + (to.G - from.G) * t), (byte)Math.Round(from.B + (to.B - from.B) * t));

        public static readonly ArenaColour[] Skins =
        {
            new ArenaColour(232, 190, 160), new ArenaColour(205, 150, 115), new ArenaColour(160, 110, 80),
            new ArenaColour(110, 75, 55), new ArenaColour(75, 52, 40)
        };

        public static readonly ArenaColour[] Hairs =
        {
            new ArenaColour(48, 36, 28), new ArenaColour(70, 50, 35), new ArenaColour(120, 90, 60),
            new ArenaColour(170, 140, 95), new ArenaColour(170, 160, 140), new ArenaColour(110, 50, 30)
        };

        /// <summary>How fast a zone's clip clock runs in this state (1 = the clip's own speed). Still stops the clock, so
        /// the fans hold the pose they are in (no jump back to a rest pose).</summary>
        public static double Motion(CrowdState state) => state == CrowdState.Still ? 0.0 : 1.0;

        /// <summary>How bright the zone's fans are in this state (1 = as lit). Still is dim (the Calm Area rules). It
        /// depends on the state alone, so the picture test sees the same dim as play.</summary>
        public static double Light(CrowdState state) => state == CrowdState.Still ? StillLight : 1.0;

        /// <summary>The Calm Area's still fans' brightness.</summary>
        public const double StillLight = 0.7;

        /// <summary>How much of their own colour and shading the zone's fans show (1 = all; less: closer to one flat dark
        /// tone). Still is low contrast (the Calm Area rules); it depends on the state alone, like Light.</summary>
        public static double Contrast(CrowdState state) => state == CrowdState.Still ? StillContrast : 1.0;

        /// <summary>The Calm Area's still fans' contrast.</summary>
        public const double StillContrast = 0.3;

        /// <summary>The one flat tone low-contrast fans go toward: the stands' own dark slate (the risers as lit), so still fans melt into them.</summary>
        public static readonly ArenaColour FlatTone = new ArenaColour(57, 61, 73);

        /// <summary>Each seat's fan's random look. neighbours: CrowdSeats.Neighbours(seats). Each fan's body type is picked
        /// from those whose outfit none of its already-dressed neighbours wears. homeJersey: the Team Skin's home jersey colour.</summary>
        public static FanLook[] Make(FanSeat[] seats, int[][] neighbours, Random random, ArenaColour homeJersey)
        {
            var looks = new FanLook[seats.Length];
            var done = new bool[seats.Length];
            var free = new List<int>(BodyTypes.Length);
            for (int i = 0; i < seats.Length; i++)
            {
                free.Clear();
                for (int b = 0; b < BodyTypes.Length; b++) free.Add(b);
                foreach (int j in neighbours[i]) if (done[j]) free.RemoveAll(b => BodyTypes[b].Base == BodyTypes[looks[j].Body].Base);
                if (free.Count == 0) throw new InvalidOperationException("a fan with every outfit worn by a dressed neighbour: " + seats[i]);
                int body = free[random.Next(free.Count)];
                BodyType type = BodyTypes[body];

                double phase = random.NextDouble();
                double height = type.MinHeight + (type.MaxHeight - type.MinHeight) * random.NextDouble();
                bool teamTop = random.NextDouble() < HomeShare;
                ArenaColour skin = Skins[random.Next(Skins.Length)];
                ArenaColour hair = Hairs[random.Next(Hairs.Length)];
                bool waves = random.NextDouble() < WaveShare;
                double g = random.NextDouble();
                int goalAct = g < SeatedOnGoalShare / 2 ? GoalAct.SeatedClap : g < SeatedOnGoalShare ? GoalAct.SeatedStill
                            : random.Next(2) == 0 ? GoalAct.FistPumpLeft : GoalAct.FistPumpRight;
                bool ohBack = random.NextDouble() < OhBackShare;
                bool claps = random.NextDouble() < ClapShare;
                looks[i] = new FanLook(phase, body, height, teamTop, teamTop ? homeJersey : default, skin, hair, waves, goalAct, ohBack, claps);
                done[i] = true;
            }
            return looks;
        }
    }
}
