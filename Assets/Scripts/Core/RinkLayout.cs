using System;
using System.Collections.Generic;

namespace HockeyGame.Core
{
    /// <summary>
    /// The Rink Layout: one source of the NHL rink's sizes and places, in metres in the scene's frame (RinkPoint: x right of
    /// the Net Face centre, y up from the ice, z toward the near net). The near goal line is the Net Face plane, z = 0; the
    /// rink grows toward the player (-z) and the far end mirrors the near end about the centre line.
    /// RinkBoards, RinkIce, ArenaStands and the far net build to these numbers. The Python art tools copy them by hand in
    /// Tools/rink_layout.py (a test checks they match): change both together.
    /// Sizes from the NHL rule book (docs/research/nhl-arena-look.md on research/nhl-arena-look); places marked "estimate" are
    /// not in the rules.
    /// </summary>
    public static class RinkLayout
    {
        const double Ft = 0.3048;

        // ---------- the rink ----------
        public const double Length = 60.96;      // 200 ft, end boards to end boards
        public const double Width = 25.91;       // 85 ft, side boards to side boards
        public const double HalfWidth = Width / 2;
        public const double CornerRadius = 8.53; // 28 ft
        public const double GoalLineFromEnd = 3.35; // 11 ft

        public const double NearGoalLineZ = 0.0;
        public const double NearEndBoardsZ = NearGoalLineZ + GoalLineFromEnd;
        public const double FarEndBoardsZ = NearEndBoardsZ - Length;
        public const double FarGoalLineZ = FarEndBoardsZ + GoalLineFromEnd;
        public const double CentreLineZ = (NearGoalLineZ + FarGoalLineZ) / 2;

        /// <summary>The same place at the other end of the rink: z mirrored about the centre line.</summary>
        public static double Mirror(double z) => 2 * CentreLineZ - z;

        // ---------- lines, circles, crease and trapezoid ----------
        /// <summary>Goal lines, crease and trapezoid lines and circles: 2 in.</summary>
        public const double ThinLine = 2 / 12.0 * Ft;
        /// <summary>Blue lines and the centre red line: 12 in.</summary>
        public const double WideLine = 1 * Ft;
        /// <summary>Middle of the near blue line: its near edge is 64 ft out from the goal line.</summary>
        public const double NearBlueLineZ = NearGoalLineZ - 64 * Ft - WideLine / 2;
        public static double FarBlueLineZ => Mirror(NearBlueLineZ);

        /// <summary>Radius of the centre circle and the four end-zone circles (15 ft).</summary>
        public const double CircleRadius = 15 * Ft;
        /// <summary>The centre spot (12 in, blue) and the other faceoff spots (2 ft, red): diameters.</summary>
        public const double CentreSpotDiameter = 1 * Ft, SpotDiameter = 2 * Ft;
        /// <summary>End-zone and neutral-zone spots sit this far either side of the middle (22 ft).</summary>
        public const double EndZoneDotX = 22 * Ft;
        /// <summary>Near end-zone spots: 20 ft out from the goal line.</summary>
        public const double NearEndZoneDotZ = NearGoalLineZ - 20 * Ft;
        /// <summary>Near neutral-zone spots: 5 ft from the blue line, in the neutral zone.</summary>
        public const double NearNeutralDotZ = NearBlueLineZ - WideLine / 2 - 5 * Ft;
        /// <summary>The referee's crease: a red half circle (10 ft radius) on the penalty box side, at the centre line.</summary>
        public const double RefereeCreaseRadius = 10 * Ft;

        /// <summary>The goal crease: 6 ft radius, 8 ft wide (1 ft outside each post).</summary>
        public const double CreaseRadius = 6 * Ft, CreaseHalfWidth = 4 * Ft;
        /// <summary>The trapezoid behind each net: 22 ft wide at the goal line, 28 ft at the end boards.</summary>
        public const double TrapezoidGoalLineWidth = 22 * Ft, TrapezoidEndBoardsWidth = 28 * Ft;

        // ---------- boards and glass (metres above the ice) ----------
        public const double BoardHeight = 1.07;   // 42 in
        public const double KickPlateTop = 0.20;
        /// <summary>The printed ad band on the board face, between the kick plate and the rail.</summary>
        public const double AdBandBottom = 0.24, AdBandTop = 0.97;
        public const double EndGlassTop = BoardHeight + 2.44;  // 8 ft of glass round the ends and corners
        public const double SideGlassTop = BoardHeight + 1.83; // 6 ft along the sides

        // ---------- the ice sheet ----------
        /// <summary>Side of the square ice sheet, centred on the centre line: past both end boards, with room behind the near net.</summary>
        public const double IceSize = 64.4;
        public const double IceCentreZ = CentreLineZ;

        // ---------- Calm Area ----------
        /// <summary>The Calm Area on the ice: x within ±CalmAreaHalfWidth (the back of the trapezoid), from the crease front to the end boards; up to the top of the view.</summary>
        public const double CalmAreaHalfWidth = 4.27;
        public const double CalmAreaFrontZ = -1.9;
        public const double CalmAreaBackZ = NearEndBoardsZ;

        // ---------- far net ----------
        /// <summary>The middle of the far net's opening, on the far goal line. It faces the near net (+z) and has no goalie.</summary>
        public static readonly RinkPoint FarNet = new RinkPoint(0, 0, FarGoalLineZ);

        // ---------- benches and penalty boxes (estimate: lengths and gaps are not in the rules) ----------
        /// <summary>An opening in the stands behind the side boards (a bench or a penalty box): on side -1 (x &lt; 0) or +1, from z to z, Depth metres behind the boards.</summary>
        public readonly struct Opening
        {
            public readonly int Side;
            public readonly double FromZ, ToZ, Depth;
            public Opening(int side, double fromZ, double toZ, double depth) { Side = side; FromZ = fromZ; ToZ = toZ; Depth = depth; }
            public override string ToString() => $"side {Side}, z {FromZ:0.##}..{ToZ:0.##}";
        }

        /// <summary>Players' benches sit on the left (shooter's view), penalty boxes on the right.</summary>
        public const int BenchSide = -1;
        public const double BenchLength = 6.1, BenchGap = 2.44, PenaltyBoxLength = 3.66, TimekeeperLength = 2.44;
        /// <summary>How far behind the boards the benches and boxes reach before the stands start.</summary>
        public const double BenchDepth = 1.8;

        /// <summary>The two players' benches, far one first.</summary>
        public static readonly Opening[] Benches = Pair(BenchSide, BenchGap / 2, BenchLength);
        /// <summary>The two penalty boxes, far one first, either side of the timekeeper.</summary>
        public static readonly Opening[] PenaltyBoxes = Pair(-BenchSide, TimekeeperLength / 2, PenaltyBoxLength);

        static Opening[] Pair(int side, double gap, double length) => new[]
        {
            new Opening(side, CentreLineZ - gap - length, CentreLineZ - gap, BenchDepth),
            new Opening(side, CentreLineZ + gap, CentreLineZ + gap + length, BenchDepth),
        };

        // ---------- the stands and the Ribbon Board ring (Tools/make_arena.py builds the stands to these) ----------
        /// <summary>Offset of the stands' front wall from the inside face of the boards, and its height.</summary>
        public const double StandsFront = 1.0, StandsFrontHeight = 1.6;
        /// <summary>The lower tier: rows of seats, each RowDepth deep and LowerRise higher than the one in front.</summary>
        public const double RowDepth = 0.85, LowerRise = 0.42;
        public const int LowerRows = 20;
        /// <summary>The Ribbon Board ring sits on the fascia between the tiers: this far out from the boards (not counting the step back beside a bench or box), this high at its bottom, this tall.</summary>
        public const double RibbonOffset = StandsFront + LowerRows * RowDepth;
        public const double RibbonBottom = StandsFrontHeight + LowerRows * LowerRise;
        public const double RibbonHeight = 1.2;
        /// <summary>The upper tier above the Ribbon Board ring: rows of seats, each RowDepth deep and UpperRise higher than the one in front.</summary>
        public const double UpperRise = 0.55;
        public const int UpperRows = 13;

        // ---------- the board outline ----------
        /// <summary>A point on the inside face of the boards, with the unit normal (InX, InZ) pointing into the rink.</summary>
        public readonly struct BoardPoint
        {
            public readonly double X, Z, InX, InZ;
            public BoardPoint(double x, double z, double inX, double inZ) { X = x; Z = z; InX = inX; InZ = inZ; }
        }

        /// <summary>x of the corner arcs' centres; their z are NearCornerZ and FarCornerZ.</summary>
        public const double CornerX = HalfWidth - CornerRadius;
        public const double NearCornerZ = NearEndBoardsZ - CornerRadius, FarCornerZ = FarEndBoardsZ + CornerRadius;
        const double EndStraight = 2 * CornerX, SideStraight = NearCornerZ - FarCornerZ;
        const double Arc = Math.PI / 2 * CornerRadius;

        /// <summary>Length of the inside face of the boards, all round.</summary>
        public const double BoardsPerimeter = 2 * EndStraight + 2 * SideStraight + 4 * Arc;

        /// <summary>
        /// The point s metres round the inside face of the boards, starting at the middle of the near end boards (behind the
        /// near net) and going toward +x, then down the +x side, across the far end and back up the -x side.
        /// </summary>
        public static BoardPoint OnBoards(double s)
        {
            s = ((s % BoardsPerimeter) + BoardsPerimeter) % BoardsPerimeter;
            double[] runs = { EndStraight / 2, Arc, SideStraight, Arc, EndStraight, Arc, SideStraight, Arc, EndStraight / 2 };
            int i = 0;
            while (i < runs.Length - 1 && s > runs[i]) s -= runs[i++];
            double a = s / CornerRadius; // angle round a corner
            switch (i)
            {
                case 0: return new BoardPoint(s, NearEndBoardsZ, 0, -1);
                case 1: return Corner(CornerX, NearCornerZ, a);
                case 2: return new BoardPoint(HalfWidth, NearCornerZ - s, -1, 0);
                case 3: return Corner(CornerX, FarCornerZ, Math.PI / 2 + a);
                case 4: return new BoardPoint(CornerX - s, FarEndBoardsZ, 0, 1);
                case 5: return Corner(-CornerX, FarCornerZ, Math.PI + a);
                case 6: return new BoardPoint(-HalfWidth, FarCornerZ + s, 1, 0);
                case 7: return Corner(-CornerX, NearCornerZ, 1.5 * Math.PI + a);
                default: return new BoardPoint(-CornerX + s, NearEndBoardsZ, 0, -1);
            }
        }

        /// <summary>A point on a corner arc about (cx, cz), angle a from the near-end direction, turning clockwise seen from above.</summary>
        static BoardPoint Corner(double cx, double cz, double a)
        {
            double ox = Math.Sin(a), oz = Math.Cos(a); // outward
            return new BoardPoint(cx + ox * CornerRadius, cz + oz * CornerRadius, -ox, -oz);
        }

        // ---------- ad panel slots ----------
        /// <summary>
        /// One printed ad panel on the board face, from FromS to ToS round the boards (OnBoards) and from AdBandBottom to
        /// AdBandTop: its middle (X, Z) on the inside face, its normal into the rink and its width along the boards. Corner
        /// panels bend round the corner arc. CalmArea panels (any part of them inside the Calm Area's bounds, behind the
        /// near net) get soft grey logos.
        /// </summary>
        public readonly struct AdPanelSlot
        {
            public readonly double FromS, ToS, X, Z, InX, InZ, Width;
            public readonly bool Corner, CalmArea;
            public AdPanelSlot(double fromS, double toS, bool corner)
            {
                BoardPoint middle = OnBoards((fromS + toS) / 2);
                FromS = fromS; ToS = toS; Width = toS - fromS; Corner = corner;
                X = middle.X; Z = middle.Z; InX = middle.InX; InZ = middle.InZ;
                CalmArea = TouchesCalmArea(fromS, toS);
            }

            /// <summary>
            /// True when any part of the boards from fromS to toS is in the Calm Area's bounds: the near end boards with x
            /// within ±CalmAreaHalfWidth, round the middle of the near end (s = 0).
            /// </summary>
            static bool TouchesCalmArea(double fromS, double toS)
            {
                double p = BoardsPerimeter;
                double from = ((fromS % p) + p) % p, to = from + (toS - fromS); // from in [0, p); the run may wrap past p
                return from <= CalmAreaHalfWidth || to >= p - CalmAreaHalfWidth;
            }
            public override string ToString() => $"ad panel at ({X:0.##}, {Z:0.##}){(CalmArea ? " in the Calm Area" : "")}";
        }

        /// <summary>Panels per end straight, per corner (curved boards, flat chords) and per side.</summary>
        public const int EndPanels = 3, CornerPanels = 4, SidePanels = 18;

        /// <summary>One row of ad panels all round the boards, corners included, in OnBoards order.</summary>
        public static readonly AdPanelSlot[] AdPanelSlots = MakeAdPanelSlots();

        static AdPanelSlot[] MakeAdPanelSlots()
        {
            var slots = new List<AdPanelSlot>();
            double s = -EndStraight / 2; // from the near end's -x corner, so the near end panels are whole
            void Run(double length, int count, bool corner)
            {
                double w = length / count;
                for (int i = 0; i < count; i++) slots.Add(new AdPanelSlot(s + w * i, s + w * (i + 1), corner));
                s += length;
            }
            for (int half = 0; half < 2; half++)
            {
                Run(EndStraight, EndPanels, false);
                Run(Arc, CornerPanels, true);
                Run(SideStraight, SidePanels, false);
                Run(Arc, CornerPanels, true);
            }
            return slots.ToArray();
        }

        // ---------- ice logo spots (estimate: broadcast look, never in an end zone) ----------
        const double BesideCircleX = 7.7, BesideCircleDz = 1.2;
        public enum IceLogoKind { CentreIce, BesideCentreCircle, NeutralZoneRound }

        /// <summary>Where an ice logo goes: its middle on the ice and its size across (x) and along (z) the rink.</summary>
        public readonly struct IceLogoSpot
        {
            public readonly IceLogoKind Kind;
            public readonly double X, Z, Width, Height;
            public IceLogoSpot(IceLogoKind kind, double x, double z, double width, double height) { Kind = kind; X = x; Z = z; Width = width; Height = height; }
            public override string ToString() => $"{Kind} logo at ({X:0.##}, {Z:0.##})";
        }

        /// <summary>
        /// The centre-ice logo (first); a logo beside the centre circle on each side, either side of the red line (near
        /// side first), clear of the circle, the red line and the referee's crease; and a round logo by each neutral-zone
        /// spot, toward centre ice.
        /// </summary>
        public static readonly IceLogoSpot[] IceLogoSpots =
        {
            new IceLogoSpot(IceLogoKind.CentreIce, 0, CentreLineZ, 6.0, 6.0),
            new IceLogoSpot(IceLogoKind.BesideCentreCircle, -BesideCircleX, CentreLineZ + BesideCircleDz, 4.2, 1.4),
            new IceLogoSpot(IceLogoKind.BesideCentreCircle, BesideCircleX, CentreLineZ + BesideCircleDz, 4.2, 1.4),
            new IceLogoSpot(IceLogoKind.BesideCentreCircle, -BesideCircleX, CentreLineZ - BesideCircleDz, 4.2, 1.4),
            new IceLogoSpot(IceLogoKind.BesideCentreCircle, BesideCircleX, CentreLineZ - BesideCircleDz, 4.2, 1.4),
            new IceLogoSpot(IceLogoKind.NeutralZoneRound, -EndZoneDotX, NearNeutralDotZ - 2.4, 2.0, 2.0),
            new IceLogoSpot(IceLogoKind.NeutralZoneRound, EndZoneDotX, NearNeutralDotZ - 2.4, 2.0, 2.0),
            new IceLogoSpot(IceLogoKind.NeutralZoneRound, -EndZoneDotX, Mirror(NearNeutralDotZ - 2.4), 2.0, 2.0),
            new IceLogoSpot(IceLogoKind.NeutralZoneRound, EndZoneDotX, Mirror(NearNeutralDotZ - 2.4), 2.0, 2.0),
        };

        // ---------- Video Board, Centre Board and roof ----------
        /// <summary>
        /// The Video Board hangs in the middle of the near end's Ribbon Board, in front of the crowd (Garrett's pick 2026-10-12:
        /// in the middle of the ribbon, in front of the crowd, so the camera barely pans up): 14.3 x 7.8 m, its middle at the
        /// ribbon's middle height, tilted toward the player. Its distance puts its frame a clear gap (about 0.8 m: the rows come in steps) in front of the nearest
        /// seat of the lower tier behind it (the stands rise toward the ribbon, so its lower edge is what the crowd's lower rows
        /// come near), so no fan or panel pokes through it or stands in front of it; the fans and panels it hides are left out
        /// (VideoBoardCover). From goalie set to the Shot it shows one dim still colour (the Calm Area rules); it is on screen
        /// at the start of the skate-in. Its top is under the light banks (20 m).
        /// </summary>
        public const double VideoBoardZ = 10.9;
        public static readonly RinkPoint VideoBoard = new RinkPoint(0, RibbonBottom + RibbonHeight / 2, VideoBoardZ);
        public const double VideoBoardWidth = 14.3, VideoBoardHeight = 7.8;
        /// <summary>The side of one LED cell on the Video Board and the Centre Board (metres): the board is a whole number of cells each way.
        /// The one place to tune how coarse the LED look is.</summary>
        public const double VideoBoardLedCell = 0.1;
        /// <summary>The dark frame round the Video Board's screen (metres each side) and its depth behind the screen.</summary>
        public const double VideoBoardFrameMargin = 0.2, VideoBoardDepth = 0.4;
        /// <summary>How far the Video Board leans forward: it faces the shooter's eye at the start of the skate-in.</summary>
        public static readonly double VideoBoardTiltDegrees =
            Math.Atan2(VideoBoard.Y - Breakaway.EyeHeight, VideoBoard.Z + Breakaway.StartDistance) * 180 / Math.PI;

        /// <summary>The Centre Board's middle (over centre ice): four screens about 11 x 6.5 m, bottom about 20 m up.</summary>
        public const double CentreBoardScreenWidth = 11.0, CentreBoardScreenHeight = 6.5, CentreBoardBottom = 20.0;
        public static readonly RinkPoint CentreBoard = new RinkPoint(0, CentreBoardBottom + CentreBoardScreenHeight / 2, CentreLineZ);
        public const double CentreBoardTop = CentreBoardBottom + CentreBoardScreenHeight;

        /// <summary>The roof's height above the ice: high enough that the Centre Board hangs under it with room (NHL arenas are well over 30 m inside).</summary>
        public const double RoofHeight = 30.0;
    }
}
