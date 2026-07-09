using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    public abstract partial class GarbusTestScene : TestScene
    {
        protected override ITestSceneTestRunner CreateRunner() => new GarbusTestSceneTestRunner();

        private partial class GarbusTestSceneTestRunner : GarbusGameBase, ITestSceneTestRunner
        {
            private TestSceneTestRunner.TestRunner runner;

            protected override void LoadAsyncComplete()
            {
                base.LoadAsyncComplete();
                Add(runner = new TestSceneTestRunner.TestRunner());
            }

            public void RunTestBlocking(TestScene test) => runner.RunTestBlocking(test);
        }
    }
}
