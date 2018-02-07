using System;
using System.Reactive.Subjects;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Xunit;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalerTest
    {
        /// <remarks>
        /// Because of the AsObservable that wraps classes in internal reactive classes it's hard to prove that the observer provided is subscribed to the subject so
        /// we prove this by calling the onnext of the observer to prove it's the one we provided.
        /// </remarks>
        [Fact]
        public void SubscribeRegistersObserverWithObservable()
        {
            var block = new PowBlock();
            var subject = new Mock<ISubject<PowBlock>>();
            var observer = new Mock<IObserver<PowBlock>>();
            subject.Setup(s => s.Subscribe(It.IsAny<IObserver<PowBlock>>()))
                .Callback<IObserver<PowBlock>>((o) =>
                {
                    o.OnNext(block);
                });

            var signaler = new Signaler<PowBlock>(subject.Object);

            var result = signaler.Subscribe(observer.Object);

            observer.Verify(v => v.OnNext(block), Times.Exactly(1));
        }

        [Fact]
        public void BroadcastSignalsSubject()
        {
            var block = new PowBlock();
            var subject = new Mock<ISubject<PowBlock>>();
            var signaler = new Signaler<PowBlock>(subject.Object);

            signaler.Broadcast(block);

            subject.Verify(s => s.OnNext(block), Times.Exactly(1));
        }
    }
}
