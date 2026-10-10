namespace HockeyGame.Core
{
    /// <summary>Which part of the net frame a Miss landed on, if any.</summary>
    public enum FrameHit
    {
        None,
        LeftPost,
        RightPost,
        Crossbar
    }

    /// <summary>
    /// The posts and crossbar round the Net Face, in Net Face coordinates (0-100 from the bottom-left, shooter's view).
    /// They stand just outside the opening, one frame thickness wide. A Shot on them is still a Miss: this only
    /// says where a Miss rang off the frame, for sound and the puck's deflection.
    /// </summary>
    public static class NetFrame
    {
        /// <summary>Net Face size and frame (post and crossbar) thickness in metres; NetFaceMapping and NetBuilder build to these.</summary>
        public const double FaceWidthMetres = 1.83;
        public const double FaceHeightMetres = 1.22;
        public const double ThicknessMetres = 0.08;

        /// <summary>Post thickness across the Net Face (x units).</summary>
        public const double PostWidth = ThicknessMetres / FaceWidthMetres * 100.0;
        /// <summary>Crossbar thickness up the Net Face (y units).</summary>
        public const double CrossbarHeight = ThicknessMetres / FaceHeightMetres * 100.0;

        /// <summary>
        /// The part of the frame at this Net Face position. Positions on the Net Face (0-100 inclusive), below the ice,
        /// or further out than the frame's thickness are None.
        /// </summary>
        public static FrameHit At(double x, double y)
        {
            bool acrossFrame = x >= -PostWidth && x <= 100.0 + PostWidth;
            bool upFrame = y >= 0.0 && y <= 100.0 + CrossbarHeight;
            if (!acrossFrame || !upFrame) return FrameHit.None;
            if (y > 100.0) return FrameHit.Crossbar;
            if (x < 0.0) return FrameHit.LeftPost;
            if (x > 100.0) return FrameHit.RightPost;
            return FrameHit.None; // on the Net Face
        }
    }
}
