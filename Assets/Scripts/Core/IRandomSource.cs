namespace HockeyGame.Core
{
    /// <summary>Source of randomness for the Round core. Next returns a value in [0, 1).</summary>
    public interface IRandomSource
    {
        double Next();
    }
}
