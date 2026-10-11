namespace HockeyGame.Core
{
    /// <summary>Where the goalie's Stance Life is at one moment (see StanceLife); each part is a share of its full size.</summary>
    public readonly struct StanceLifeMoment
    {
        /// <summary>0 breathed out (the plain Ready Stance) .. 1 breathed in: the chest rises a little.</summary>
        public readonly double Breath;
        /// <summary>-1..1: his weight over his right (-) or left (+) skate; the hips move across, the skates stay put.</summary>
        public readonly double Shift;
        /// <summary>0..1: the stick blade lifted off the ice for a tap (two quick touches).</summary>
        public readonly double Tap;

        public StanceLifeMoment(double breath, double shift, double tap) { Breath = breath; Shift = shift; Tap = tap; }
    }

    /// <summary>
    /// Stance Life (#44): the small, steady motion of a goalie waiting in the Ready Stance (breathing, weight shifts, now and
    /// then a stick tap), so he is never a statue. A pure function of a clock alone: it never depends on the Set-up, the
    /// Attack Side or the Shot, so it gives no hint, and the same moment always looks the same. At time 0 it is the plain
    /// Ready Stance.
    /// </summary>
    public static class StanceLife
    {
        /// <summary>One breath (in and out), seconds.</summary>
        public const double BreathSeconds = 3.4;
        /// <summary>One weight shift (onto one skate, the other and back), seconds; its size swells and fades over SwellSeconds.</summary>
        public const double ShiftSeconds = 7.3, SwellSeconds = 13.1;
        /// <summary>The weight shift's size swells and fades between SwellMiddle - SwellDepth and SwellMiddle + SwellDepth of full.</summary>
        public const double SwellMiddle = 0.6, SwellDepth = 0.4;
        /// <summary>Time is cut into slots this long; a slot holds at most one tap (two touches over TapSeconds).</summary>
        public const double TapSlotSeconds = 3.6, TapSeconds = 0.7;
        /// <summary>The share of slots that hold a tap.</summary>
        public const double TapShare = 0.55;
        /// <summary>A tap starts at least this long into its slot (seconds).</summary>
        public const double TapLead = 0.3;

        public static StanceLifeMoment At(double seconds)
        {
            double breath = 0.5 - 0.5 * System.Math.Cos(2 * System.Math.PI * seconds / BreathSeconds);
            double shift = System.Math.Sin(2 * System.Math.PI * seconds / ShiftSeconds)
                * (SwellMiddle + SwellDepth * System.Math.Sin(2 * System.Math.PI * seconds / SwellSeconds));
            return new StanceLifeMoment(breath, shift, Tap(seconds));
        }

        static double Tap(double seconds)
        {
            long slot = (long)System.Math.Floor(seconds / TapSlotSeconds);
            if (Hash(slot, 1) >= TapShare) return 0;
            // Starts somewhere in the slot, leaving room for the tap to end inside it.
            double start = slot * TapSlotSeconds + TapLead + Hash(slot, 2) * (TapSlotSeconds - TapSeconds - TapLead);
            double u = (seconds - start) / TapSeconds;
            if (u <= 0 || u >= 1) return 0;
            double s = System.Math.Sin(2 * System.Math.PI * u);
            return s * s;
        }

        /// <summary>A steady 0..1 value for a slot (the same slot always gives the same value).</summary>
        static double Hash(long slot, int salt)
        {
            unchecked
            {
                ulong h = (ulong)slot * 0x9E3779B97F4A7C15UL + (ulong)salt * 0xBF58476D1CE4E5B9UL;
                h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 27; h *= 0x94D049BB133111EBUL;
                h ^= h >> 31;
                return (h >> 11) * (1.0 / (1UL << 53));
            }
        }
    }
}
