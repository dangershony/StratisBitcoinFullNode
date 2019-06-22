using System;
using System.Threading;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure
{
	public class LongRunningOperationContext
	{
		readonly EncryptionProgress _encryptionProgress;
		readonly CancellationToken _cancellationToken;

		public LongRunningOperationContext(CancellationToken token, EncryptionProgress encryptionProgress)
		{
			if(encryptionProgress == null)
				throw new ArgumentNullException("encryptionProgress");
			this._encryptionProgress = encryptionProgress;
			this._cancellationToken = token;
		}

		public EncryptionProgress EncryptionProgress
		{
			get { return this._encryptionProgress; }
		}

		public CancellationToken CancellationToken
		{
			get { return this._cancellationToken; }
		}
	}
}