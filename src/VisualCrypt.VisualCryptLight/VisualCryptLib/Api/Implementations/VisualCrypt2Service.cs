using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces;
using VisualCrypt.VisualCryptLight.VisualCryptLib.ECC;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Implementations
{
	public class VisualCrypt2Service : IVisualCrypt2Service
	{
		VisualCryptAPI2Internal _internal;
		IPlatform _platform;
		bool _log = false;

		public void Init(IPlatform platform, string name = "VisualCrypt2Service Default Instance")
		{
			if (this._platform != null)
				throw new InvalidOperationException("An instance of IPlatform has already been supplied.");
			this._platform = platform;
			this.Name = name;
			this._internal = new VisualCryptAPI2Internal(this._platform);
			this.SymmetricKeyRepository = new SymmetricKeyRepository();
		}

		public string Name { get; private set; }

		public SymmetricKeyRepository SymmetricKeyRepository { get; private set; }

		public Response<KeyMaterial64> HashPassword(NormalizedPassword normalizedPassword)
		{
			var response = new Response<KeyMaterial64>();

			try
			{
				Guard.NotNull(normalizedPassword);
				EnsurePlatform();

				var utf16LeBytes = Encoding.Unicode.GetBytes(normalizedPassword.Text);

				var sha512 = this._platform.ComputeSHA512(utf16LeBytes);

				response.Result = new KeyMaterial64(sha512);
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<CipherV2> Encrypt(Cleartext cleartext, KeyMaterial64 keyMaterial64, RoundsExponent roundsExponent,
			LongRunningOperationContext context)
		{
			var response = new Response<CipherV2>();

			try
			{
				Guard.NotNull(new object[] { cleartext, keyMaterial64, roundsExponent });
				EnsurePlatform();

				Compressed compressed = this._internal.Compress(cleartext);

				var cipherV2 = EncryptCommon(keyMaterial64, roundsExponent, context, compressed);

				response.Result = cipherV2;
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		CipherV2 EncryptCommon(KeyMaterial64 keyMaterial64, RoundsExponent roundsExponent, LongRunningOperationContext context,
			Compressed compressed)
		{
			if (context == null)
				context = new LongRunningOperation(progress => { }, () => { }).Context;

			if (this._log)
			{
				Debug.WriteLine("KeyMaterial64:");
				Debug.WriteLine(keyMaterial64.GetBytes().ToHexView(false));
			}

			if (this._log)
			{
				Debug.WriteLine("Compressed:");
				Debug.WriteLine(compressed.GetBytes().ToHexView(false));
			}

			PaddedData paddedData = this._internal.ApplyRandomPadding(compressed);
			if (this._log)
			{
				Debug.WriteLine("PaddedData:");
				Debug.WriteLine(paddedData.GetBytes().ToHexView(false));

				Debug.WriteLine("PlainTextPadding:");
				Debug.WriteLine(paddedData.PlaintextPadding);
			}

			IV16 iv = new IV16(this._platform.GenerateRandomBytes(16));
			if (this._log)
			{
				Debug.WriteLine("IV16:");
				Debug.WriteLine(iv.GetBytes().ToHexView(false));
			}

			InputDerivedKey32 inputDerivedKey = roundsExponent.Value == RoundsExponent.DontMakeRounds
				? CreateDerivedKeyWithSHA256(iv, keyMaterial64)
				: CreatePasswordDerivedKeyWithBCrypt(iv, keyMaterial64, roundsExponent, context);

			if (this._log)
			{
				Debug.WriteLine("InputDerivedKey32:");
				Debug.WriteLine(inputDerivedKey.GetBytes().ToHexView(false));
			}

			RandomKey32 randomKey = new RandomKey32(this._platform.GenerateRandomBytes(32));
			if (this._log)
			{
				Debug.WriteLine("RandomKey32:");
				Debug.WriteLine(randomKey.GetBytes().ToHexView(false));
			}

			VisualCryptAPI2Internal.IVCache ivCache = roundsExponent.Value == RoundsExponent.DontMakeRounds
				? null
				: this._internal.CreateIVTable(iv, roundsExponent.Value);

			var cipherV2 = new CipherV2 { RoundsExponent = roundsExponent, IV16 = iv };
			this._internal.AESEncryptRandomKeyWithInputDerivedKey(inputDerivedKey, randomKey, cipherV2, ivCache, context);
			if (this._log)
			{
				Debug.WriteLine("RandomKeyCipher32:");
				Debug.WriteLine(cipherV2.RandomKeyCipher32.GetBytes().ToHexView(false));
			}

			this._internal.AESEncryptMessageWithRandomKey(paddedData, randomKey, cipherV2, ivCache, context);
			if (this._log)
			{
				Debug.WriteLine("MessageCipher:");
				Debug.WriteLine(cipherV2.MessageCipher.GetBytes().ToHexView(false));
			}

			MAC16 mac = CreateMAC(cipherV2, context);
			if (this._log)
			{
				Debug.WriteLine("MAC16:");
				Debug.WriteLine(mac.GetBytes().ToHexView(false));
			}

			this._internal.AESEncryptMACWithRandomKey(cipherV2, mac, randomKey, ivCache, context);
			if (this._log)
			{
				Debug.WriteLine("MACCipher16:");
				Debug.WriteLine(cipherV2.MACCipher16.GetBytes().ToHexView(false));
			}
			return cipherV2;
		}

		public Response<CipherV2> BinaryEncrypt(Clearbytes clearBytes, KeyMaterial64 keyMaterial64, RoundsExponent roundsExponent, LongRunningOperationContext context)
		{
			var response = new Response<CipherV2>();

			try
			{
				Guard.NotNull(new object[] { clearBytes, keyMaterial64, roundsExponent });
				EnsurePlatform();

				Compressed compressed = this._internal.CompressBytes(clearBytes);

				var cipherV2 = EncryptCommon(keyMaterial64, roundsExponent, context, compressed);
				response.Result = cipherV2;
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		MAC16 CreateMAC(CipherV2 cipherV2, LongRunningOperationContext context)
		{
			// Create the MAC only for items that, while decrypting, have not been used up to this point but do include the version.
			var securables = ByteArrays.Concatenate(cipherV2.MessageCipher.GetBytes(), new[] { cipherV2.PlaintextPadding.Value },
				new[] { CipherV2.Version });

			// See e.g. http://csrc.nist.gov/publications/fips/fips180-4/fips-180-4.pdf Chapter 7 for hash truncation.
			byte[] truncatedHash = new byte[16];
			if (cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds)
			{
				var sha256 = this._platform.ComputeSHA256(securables);
				Buffer.BlockCopy(sha256, 0, truncatedHash, 0, 16);
			}
			else
			{
				context.EncryptionProgress.Message = LocalizableStrings.MsgCalculatingMAC;
				var bCrypt24 = BCrypt.CreateHash(cipherV2.IV16, securables, cipherV2.RoundsExponent.Value, context);
				Buffer.BlockCopy(bCrypt24.GetBytes(), 0, truncatedHash, 0, 16);
			}
			return new MAC16(truncatedHash);
		}


		InputDerivedKey32 CreatePasswordDerivedKeyWithBCrypt(IV16 iv, KeyMaterial64 keyMaterial64, RoundsExponent roundsExponent,
			LongRunningOperationContext context)
		{

			var leftSHA512 = new byte[32];
			var rightSHA512 = new byte[32];
			Buffer.BlockCopy(keyMaterial64.GetBytes(), 0, leftSHA512, 0, 32);
			Buffer.BlockCopy(keyMaterial64.GetBytes(), 32, rightSHA512, 0, 32);

			context.EncryptionProgress.Message = LocalizableStrings.MsgProcessingKey;

			// Compute the left side on a ThreadPool thread
			var task = Task.Run(() => BCrypt.CreateHash(iv, leftSHA512, roundsExponent.Value, context));

			// Compute the right side after dispatching the work for the right side
			BCrypt24 rightBCrypt = BCrypt.CreateHash(iv, rightSHA512, roundsExponent.Value, context);

			// Wait for the left side result
			task.Wait(context.CancellationToken);

			// Use the results
			var combinedHashes = ByteArrays.Concatenate(keyMaterial64.GetBytes(), task.Result.GetBytes(), rightBCrypt.GetBytes());
			Debug.Assert(combinedHashes.Length == 64 + 24 + 24);

			var condensedHash = this._platform.ComputeSHA256(combinedHashes);
			return new InputDerivedKey32(condensedHash);
		}

		InputDerivedKey32 CreateDerivedKeyWithSHA256(IV16 iv, KeyMaterial64 keyMaterial64)
		{
			var keyMaterial = ByteArrays.Concatenate(iv.GetBytes(), keyMaterial64.GetBytes());
			var derivedKey = this._platform.ComputeSHA256(keyMaterial);
			return new InputDerivedKey32(derivedKey);
		}

		public Response<VisualCryptText> EncodeVisualCrypt(CipherV2 cipherV2)
		{
			var response = new Response<VisualCryptText>();

			try
			{
				Guard.NotNull(cipherV2);
				EnsurePlatform();

				response.Result = VisualCrypt2Formatter.CreateVisualCryptText(cipherV2);
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<CipherV2> DecodeVisualCrypt(string visualCryptText, LongRunningOperationContext context)  // should the parameter type be VisualCryptText? 
		{
			var response = new Response<CipherV2>();

			try
			{
				Guard.NotNull(visualCryptText);
				EnsurePlatform();

				response.Result = VisualCrypt2Formatter.DissectVisualCryptText(visualCryptText, context);
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<Cleartext> Decrypt(CipherV2 cipherV2, KeyMaterial64 keyMaterial64, LongRunningOperationContext context)
		{
			var response = new Response<Cleartext>();

			try
			{
				Compressed compressed = DecryptCommon(cipherV2, keyMaterial64, context);

				Cleartext cleartext = this._internal.Decompress(compressed);

				response.Result = cleartext;
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<Clearbytes> BinaryDecrypt(CipherV2 cipherV2, KeyMaterial64 keyMaterial64, LongRunningOperationContext context)
		{
			var response = new Response<Clearbytes>();

			try
			{
				Compressed compressed = DecryptCommon(cipherV2, keyMaterial64, context);

				Clearbytes cleartext = this._internal.DecompressBytes(compressed);

				response.Result = cleartext;
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		Compressed DecryptCommon(CipherV2 cipherV2, KeyMaterial64 keyMaterial64, LongRunningOperationContext context)
		{
			if (context == null)
				context = new LongRunningOperation(progress => { }, () => { }).Context;

			Guard.NotNull(new object[] { cipherV2, keyMaterial64 });
			EnsurePlatform();

			InputDerivedKey32 inputDerivedKey = cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds
				? CreateDerivedKeyWithSHA256(cipherV2.IV16, keyMaterial64)
				: CreatePasswordDerivedKeyWithBCrypt(cipherV2.IV16, keyMaterial64, cipherV2.RoundsExponent, context);

			VisualCryptAPI2Internal.IVCache ivCache = cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds
				? null
				: this._internal.CreateIVTable(cipherV2.IV16, cipherV2.RoundsExponent.Value);

			RandomKey32 randomKey = this._internal.AESDecryptRandomKeyWithPasswordDerivedKey(cipherV2, inputDerivedKey, ivCache, context);

			MAC16 decryptedMAC = this._internal.AESDecryptMAC(cipherV2, randomKey, ivCache, context);

			MAC16 actualMAC = CreateMAC(cipherV2, context);

            if (!actualMAC.GetBytes().SequenceEqual(decryptedMAC.GetBytes()))
				throw new AuthenticationFailedException(LocalizableStrings.MsgPasswordError);

			PaddedData paddedData = this._internal.AESDecryptMessage(cipherV2, cipherV2.IV16, randomKey, ivCache, context);

			Compressed compressed = this._internal.RemovePadding(paddedData);
			return compressed;
		}

		public Response<string> SuggestRandomPassword()
		{
			var response = new Response<string>();

			try
			{
				EnsurePlatform();
				response.Result = this._internal.GenerateRandomPassword();
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		/// <summary>
		/// Removes all leading and trailing Unicode whitespace characters and replaces the remaining whitespace characters
		/// with u\0020 space characters. Adjacent whitespace is condensed to a single u\0020 character. Other Unicode
		/// control characters are stripped completely.
		/// The control characters are specifically the Unicode values U+0000 to U+001F and U+007F to U+009F;
		/// whitespace characters as defined by Char.IsWhiteSpace in .net 4.5.
		/// </summary>
		/// <param name="rawPassword">The password string obtained from textBox.Text.</param>
		/// <returns>The sanitized UTF-16 password string, the bytes of which are used as input for the password hash function.</returns>
		/// <see cref="http://www.unicode.org/Public/UNIDATA/UnicodeData.txt"/>
		public Response<NormalizedPassword> NormalizePassword(string rawPassword)
		{
			var response = new Response<NormalizedPassword>();

			try
			{
				Guard.NotNull(rawPassword);
				EnsurePlatform();


				// from msdn: White-space characters are defined by the Unicode standard. 
				// The Trim() method removes any leading and trailing characters that produce 
				// a return value of true when they are passed to the Char.IsWhiteSpace method.
				string sanitized =
					rawPassword
						.FilterNonWhitespaceControlCharacters()
						.CondenseAndNormalizeWhiteSpace().
						Trim();

				response.Result = new NormalizedPassword(sanitized);
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		void EnsurePlatform()
		{
			if (this._platform == null || this._internal == null)
				throw new InvalidOperationException("You must supply an instance of IPlatform before you can use the service.");
		}

		public Response<byte[]> BinaryEncodeVisualCrypt(CipherV2 cipherV2, LongRunningOperationContext context)
		{
			var response = new Response<byte[]>();
			try
			{
				Guard.NotNull(cipherV2);
				EnsurePlatform();
				context?.CancellationToken.ThrowIfCancellationRequested();
				response.Result = VisualCrypt2Formatter.CreateBinary(cipherV2);
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<CipherV2> BinaryDecodeVisualCrypt(byte[] visualCryptBytes, LongRunningOperationContext context)
		{
			var response = new Response<CipherV2>();
			try
			{
				Guard.NotNull(visualCryptBytes);
				EnsurePlatform();
				context?.CancellationToken.ThrowIfCancellationRequested();
				response.Result = VisualCrypt2Formatter.DissectVisualCryptBytes(visualCryptBytes, context);
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<QualifiedRandom> GetRandom(int randomLenght, byte[] seed = null)
		{
			var response = new Response<QualifiedRandom>();
			try
			{
				EnsurePlatform();

				var randomBytes = this._platform.GenerateRandomBytes(randomLenght);
				randomBytes = this._platform.ComputeSHA512(randomBytes);

				if (seed != null)
				{
					seed = this._platform.ComputeSHA512(seed);
					var combinedBytes = ByteArrays.Concatenate(randomBytes, seed);
					randomBytes = this._platform.ComputeSHA512(combinedBytes);
				}
				response.Result = new QualifiedRandom { X = randomBytes.Take(randomLenght).ToArray() };
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<QualifiedRandom> TestRandomNumberGeneration(int sampleSize, int randomLenght)
		{
			var response = new Response<QualifiedRandom>();
			try
			{
				EnsurePlatform();
				response.Result = this._internal.TestRandomNumberGeneration(sampleSize, randomLenght);
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		public Response<ECKeyPair> GenerateECKeyPair(byte[] seed = null)
		{
			var response = new Response<ECKeyPair>();
			try
			{
				var randomResponse = GetRandom(32, seed);
				if (!randomResponse.IsSuccess)
					throw new Exception(response.Error);
				var privateKeyToBeClamped = randomResponse.Result.X;
				Curve25519.ClampPrivateKeyInline(privateKeyToBeClamped);
				var publicKey = Curve25519.GetPublicKey(privateKeyToBeClamped);
				var keyPair = new ECKeyPair { PrivateKey = privateKeyToBeClamped, PublicKey = publicKey };
				response.Result = keyPair;
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e);
			}
			return response;
		}

		

		public byte[] CalculateAndHashSharedSecret(byte[] privateKey, byte[] publicKey)
		{
			Guard.NotNull(privateKey, publicKey);
			var sharedSecret = Curve25519.GetSharedSecret(privateKey, publicKey);
			var sharedSecretHash = this._platform.ComputeSHA256(sharedSecret);
			return sharedSecretHash;
		}

		public byte[] DefaultEncrypt(byte[] plaintextBytes, KeyMaterial64 keyMaterial64)
		{
			var binaryEncryptResponse = BinaryEncrypt(new Clearbytes(plaintextBytes), keyMaterial64, new RoundsExponent(RoundsExponent.DontMakeRounds), null);
			if(binaryEncryptResponse.IsSuccess)
				return VisualCrypt2Formatter.CreateBinary(binaryEncryptResponse.Result);
			throw new Exception(binaryEncryptResponse.Error);
		}

		public byte[] DefaultDecrypt(byte[] cipherTextBytes, KeyMaterial64 keyMaterial64, LongRunningOperationContext context = null)
		{
			CipherV2 cipherV2 = VisualCrypt2Formatter.DissectVisualCryptBytes(cipherTextBytes, context);
			var binaryDecryptResponse = BinaryDecrypt(cipherV2, keyMaterial64, context);
			if (!binaryDecryptResponse.IsSuccess)
				throw new Exception(binaryDecryptResponse.Error);
			return binaryDecryptResponse.Result.GetBytes();
		}

		public BigInteger GetPositive520BitInteger()
		{

			var bytes = GetRandom(65).Result.X;
			var number = new BigInteger(bytes);
			if (number == BigInteger.Zero || number == BigInteger.One)
				return GetPositive520BitInteger();
			if (number.Sign != 1)
				number = number * -1;

			return number;
		}
	}
}