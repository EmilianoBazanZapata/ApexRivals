using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.Environment
{
    /// <summary>Retains focused environment-test results across Play Mode domain reloads.</summary>
    [InitializeOnLoad]
    public static class EnvironmentTestRunner
    {
        private const string Key = "ApexRivals.EnvironmentTests";
        private static TestRunnerApi _api;
        static EnvironmentTestRunner() => TestRunnerApi.RegisterTestCallback(new Results());

        public static string Status => SessionState.GetString(Key, "No environment test run.");

        public static void Run(bool playMode, string[] names)
        {
            if (SessionState.GetBool(Key + ".Running", false)) throw new System.InvalidOperationException("Tests already running.");
            SessionState.SetBool(Key + ".Running", true);
            SessionState.SetString(Key, "Running");
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.Execute(new ExecutionSettings(new Filter { testMode = playMode ? TestMode.PlayMode : TestMode.EditMode, testNames = names })
                { runSynchronously = !playMode });
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Key + ".Running", false)) return;
                var failures = new List<string>();
                Collect(result, failures);
                SessionState.SetString(Key, "Passed=" + result.PassCount + "; Failed=" + result.FailCount + "; Skipped=" + result.SkipCount +
                    "; " + string.Join("\n", failures));
                SessionState.SetBool(Key + ".Running", false);
                if (_api != null) Object.DestroyImmediate(_api);
                _api = null;
            }
            private static void Collect(ITestResultAdaptor result, List<string> failures)
            {
                if (!result.HasChildren && result.FailCount > 0) failures.Add(result.Test.FullName + ": " + result.Message + "\n" + result.StackTrace);
                foreach (var child in result.Children) Collect(child, failures);
            }
        }
    }
}
