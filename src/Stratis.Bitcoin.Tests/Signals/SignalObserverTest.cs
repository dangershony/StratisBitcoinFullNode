using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Logging;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalObserverTest : LogsTestBase
    {
        private SignalObserver<PowBlock> observer;

        public SignalObserverTest()
        {
            this.observer = new TestBlockSignalObserver();
        }

        // the log was removed from the observer
        //[Fact]
        public void SignalObserverLogsSignalOnError()
        {
            var exception = new InvalidOperationException("This should not have occurred!");

            this.observer.OnError(exception);

            this.AssertLog(this.FullNodeLogger, LogLevel.Error, exception.ToString());
        }

        private class TestBlockSignalObserver : SignalObserver<PowBlock>
        {
            public TestBlockSignalObserver()
            {
            }

            protected override void OnNextCore(PowBlock value)
            {
            }
        }
    }
}
