namespace HockeyGame.Core
{
    /// <summary>Combines only one visible adjacent pair from the same outfit/detail/tier coarse sector.</summary>
    public static class VisibleCrowdPair
    {
        public static bool CanMerge(int key, int group, int start, int count,
            int nextKey, int nextGroup, int nextStart, bool visible, bool nextVisible) =>
            visible && nextVisible && key >= 0 && nextKey == key + 1 && key / 2 == nextKey / 2 &&
            group == nextGroup && start >= 0 && count > 0 && (long)start + count == nextStart;
    }
}
