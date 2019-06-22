using System;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces
{
	public interface IEncryptionProgress : IProgress<EncryptionProgress>
	{
		int Percent { get; set; }

		string Message { get; set; }
	}
}