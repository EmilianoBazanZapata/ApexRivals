namespace ApexRivals.SceneFlow.Runtime
{
    public sealed class RaceSessionState
    {
        private RaceSessionResult _result;

        public bool HasResult { get; private set; }
        public RaceSessionResult Result => _result;

        public void SetResult(RaceSessionResult result)
        {
            _result = result;
            HasResult = true;
        }

        public void Clear()
        {
            _result = default;
            HasResult = false;
        }
    }
}
