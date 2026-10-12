using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>One of the crowd's six body types: which of the two bodies (Base: 0 man, 1 woman), which of its baked
    /// shapes (Shape: 0 regular, 1 heavy, 2 slim; Tools/make_crowd.py BODY_TYPES) and its height range (1 = as made).</summary>
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
    /// CrowdLooks.BodyTypes) and height, its shirt, skin and hair (or cap) colours, its hair style (CrowdLooks.HairStyle)
    /// and whether it is one of the fans who wave in the skate-in.</summary>
    public readonly struct FanLook
    {
        public readonly double Phase, Height;
        public readonly int Body, HairStyle;
        public readonly ArenaColour Shirt, Skin, Hair;
        public readonly bool Waves;

        public FanLook(double phase, int body, double height, ArenaColour shirt, ArenaColour skin, ArenaColour hair, int hairStyle, bool waves)
        {
            Phase = phase; Body = body; Height = height; Shirt = shirt; Skin = skin; Hair = hair; HairStyle = hairStyle; Waves = waves;
        }
    }

    /// <summary>
    /// How the 3D crowd looks: what the fans in a zone do for each Arena Show crowd state, and each fan's random look.
    /// Neighbouring fans never share a body type, so no two neighbours look alike.
    /// </summary>
    public static class CrowdLooks
    {
        /// <summary>The share of fans in the home jersey; the rest wear dark plain clothes.</summary>
        public const double HomeShare = 0.4;
        /// <summary>The share of fans who wave in the skate-in; the rest sit and make small moves.</summary>
        public const double WaveShare = 0.3;

        /// <summary>The six body types, as variants of the two free Quaternius bodies.</summary>
        public static readonly BodyType[] BodyTypes =
        {
            new BodyType("man", 0, 0, 0.97, 1.03),
            new BodyType("big man", 0, 1, 0.97, 1.03),
            new BodyType("teen boy", 0, 2, 0.86, 0.92),
            new BodyType("woman", 1, 0, 0.95, 1.01),
            new BodyType("big woman", 1, 1, 0.95, 1.01),
            new BodyType("teen girl", 1, 2, 0.85, 0.91),
        };

        /// <summary>Hair styles. Cropped: the hair colour on the scalp only. HairMesh1..3: the body's three hair meshes
        /// (man: buzzed, parted, cropped with a beard; woman: buzzed, long, buns) over a hair-coloured scalp. Cap: a cap in
        /// a cap colour. Bald: skin on the scalp (men only).</summary>
        public static class HairStyle
        {
            public const int Cropped = 0, HairMesh1 = 1, HairMesh2 = 2, HairMesh3 = 3, Cap = 4, Bald = 5;
        }

        /// <summary>The home jersey: a placeholder burgundy until the Team Skin holds it.</summary>
        public static readonly ArenaColour HomeJersey = new ArenaColour(112, 30, 46);

        /// <summary>Dark plain clothes: charcoal, brown, olive, plum and slate (no black, blue or red).</summary>
        public static readonly ArenaColour[] DarkPlain =
        {
            new ArenaColour(50, 50, 54), new ArenaColour(62, 46, 36), new ArenaColour(52, 56, 40),
            new ArenaColour(56, 40, 56), new ArenaColour(40, 46, 54)
        };

        /// <summary>Everyone's pants (dark denim) and shoes (dark brown): dark, but not puck black.</summary>
        public static readonly ArenaColour Pants = new ArenaColour(40, 42, 52), Shoes = new ArenaColour(52, 40, 32);

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

        /// <summary>Cap colours: the home jersey and dark plain ones.</summary>
        public static readonly ArenaColour[] Caps =
        {
            HomeJersey, new ArenaColour(50, 50, 54), new ArenaColour(52, 56, 40), new ArenaColour(70, 56, 44)
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
        /// from those none of its already-dressed neighbours has.</summary>
        public static FanLook[] Make(FanSeat[] seats, int[][] neighbours, Random random)
        {
            var looks = new FanLook[seats.Length];
            var done = new bool[seats.Length];
            var free = new List<int>(BodyTypes.Length);
            for (int i = 0; i < seats.Length; i++)
            {
                free.Clear();
                for (int b = 0; b < BodyTypes.Length; b++) free.Add(b);
                foreach (int j in neighbours[i]) if (done[j]) free.Remove(looks[j].Body);
                if (free.Count == 0) throw new InvalidOperationException("a fan with six dressed neighbours: " + seats[i]);
                int body = free[random.Next(free.Count)];
                BodyType type = BodyTypes[body];

                double phase = random.NextDouble();
                double height = type.MinHeight + (type.MaxHeight - type.MinHeight) * random.NextDouble();
                ArenaColour shirt = random.NextDouble() < HomeShare ? HomeJersey : DarkPlain[random.Next(DarkPlain.Length)];
                ArenaColour skin = Skins[random.Next(Skins.Length)];
                int style = HairFor(type, random);
                ArenaColour hair = style == HairStyle.Cap ? Caps[random.Next(Caps.Length)] : Hairs[random.Next(Hairs.Length)];
                bool waves = random.NextDouble() < WaveShare;
                looks[i] = new FanLook(phase, body, height, shirt, skin, hair, style, waves);
                done[i] = true;
            }
            return looks;
        }

        static int HairFor(BodyType type, Random random)
        {
            double r = random.NextDouble();
            if (r < 0.15) return HairStyle.Cap;
            if (type.Base == 0 && r < 0.25) return HairStyle.Bald;
            if (r < 0.35) return HairStyle.Cropped;
            return HairStyle.HairMesh1 + random.Next(3);
        }
    }
}
