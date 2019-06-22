using System;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Implementations
{
	public class SymmetricKeyRepository
	{
		KeyMaterial64 _masterRandomKey;

		public KeyMaterial64 GetMasterRandomKey()
		{
			if (this._masterRandomKey == null)
				throw new InvalidOperationException("SymmetricKeyRepository: _masterRandomKey is null.");
			return this._masterRandomKey;
		}

		public void SetMasterRandomKey(KeyMaterial64 masterRandomKey)
		{
			if (masterRandomKey == null)
				throw new ArgumentNullException(nameof(masterRandomKey));
			this._masterRandomKey = masterRandomKey;
		}

		public void ClearMasterRandomKey()
		{
			if (this._masterRandomKey != null)
				this._masterRandomKey.GetBytes().FillWithZeros();
			this._masterRandomKey = null;
		}

	}
}