using System;
using System.Diagnostics;
using System.Threading;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure
{
    public sealed class LongRunningOperation : IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly LongRunningOperationContext _context;
        readonly Action _switchbackToPreviousBar;

        public LongRunningOperation()
        {
            this._cancellationTokenSource = new CancellationTokenSource();
            this._switchbackToPreviousBar = () => { Debug.WriteLine("No switch-back delegate set!"); };
            this._context = new LongRunningOperationContext(this._cancellationTokenSource.Token, new EncryptionProgress(p =>
            {
                Debug.WriteLine("No reporting delegate set!.");
            }));
        }
        public LongRunningOperation(Action<EncryptionProgress> reportAction, Action switchbackToPreviousBar)
        {
            this._cancellationTokenSource = new CancellationTokenSource();
            this._switchbackToPreviousBar = switchbackToPreviousBar;
            this._context = new LongRunningOperationContext(this._cancellationTokenSource.Token, new EncryptionProgress(reportAction));
        }

        public LongRunningOperationContext Context
        {
            get { return this._context; }
        }

        public void CheckCanceled()
        {

            this._cancellationTokenSource.Token.ThrowIfCancellationRequested();
        }

        public void Cancel()
        {

            if (!this._isDispsed && this._cancellationTokenSource.Token.CanBeCanceled)
                this._cancellationTokenSource.Cancel();
            this._switchbackToPreviousBar();
        }

        bool _isDispsed;
        public void Dispose()
        {
            if (!this._isDispsed)
                this._cancellationTokenSource.Dispose();
            this._isDispsed = true;
        }
    }
}