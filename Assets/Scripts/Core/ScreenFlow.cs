namespace HockeyGame.Core
{
    public enum FlowScreen
    {
        Start,
        Playing,
        End
    }

    public sealed class ScreenFlow
    {
        private readonly IRandomSource _random;
        private readonly SaveData _save;

        public ScreenFlow(IRandomSource random, SaveData save)
        {
            _random = random;
            _save = save;
            Round = new Round(random, save.ToRoundSettings(), save.BestScore);
        }

        public FlowScreen Screen { get; private set; }

        public Round Round { get; private set; }

        public void Press()
        {
            if (Screen == FlowScreen.Start)
            {
                Round = new Round(_random, _save.ToRoundSettings(), _save.BestScore);
                Round.StartRound();
                Screen = FlowScreen.Playing;
            }
        }

        public void SubmitShot(double x, double y, double speedKmh, bool isTestShot)
        {
            if (Screen == FlowScreen.Playing)
                Round.SubmitShot(x, y, speedKmh, isTestShot);
            else
                Press();
        }

        public void Tick(double realSeconds)
        {
            if (Screen == FlowScreen.Playing)
                Round.Tick(realSeconds);
        }
    }
}
