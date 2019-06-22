using System;
using System.Linq;
using System.Threading.Tasks;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.E2E
{
	public enum E2EDecryptionKeyType
	{
		MyStaticPrivateKey = 1,
		DynamicPrivateKey = 2,
		UnavailableDynamicPrivateKey =3
	}
	public class GetE2EDecryptionKeyResult
	{
		public KeyMaterial64 E2EDecryptionKeyMaterial;
		public E2EDecryptionKeyType E2EDecryptionKeyType;
	}

	public class E2ERatchet
	{

		const int KeepLatestDynamicPrivateKeys = 50;

		readonly IVisualCrypt2Service _visualCrypt2Service;
		readonly byte[] _myStaticPrivateKey;
		readonly Func<string, Task<E2EUser>> _gu;  // use GetCheckedUser()
		readonly Func<E2EUser, Task> _updateUser;
		readonly RatchetTimer _ratchetTimer = new RatchetTimer();

		public E2ERatchet(string myId, byte[] myStaticPrivateKey, IVisualCrypt2Service visualCrypt2Service, Func<string, Task<E2EUser>> getUser, Func<E2EUser, Task> updateUser)
		{
			this.MyId = myId;
			this._myStaticPrivateKey = myStaticPrivateKey;
			this._visualCrypt2Service = visualCrypt2Service;
			this._gu = getUser;
			this._updateUser = updateUser;
		}

		public string MyId { get; }


		public async Task<GetE2EDecryptionKeyResult> GetEndToEndDecryptionKey(string senderId, byte[] dynamicPublicKey, long privateKeyHint)
		{
			var user = await GetCheckedUser(senderId);

			var result = new GetE2EDecryptionKeyResult();
			

			byte[] dynamicPrivateKeyOrStaticPrivateKey;

			if (privateKeyHint == 0)
			{
				dynamicPrivateKeyOrStaticPrivateKey = this._myStaticPrivateKey;
				result.E2EDecryptionKeyType = E2EDecryptionKeyType.MyStaticPrivateKey;
			}
			else
			{
				if (user.DynamicPrivateDecryptionKeys.TryGetValue(privateKeyHint, out dynamicPrivateKeyOrStaticPrivateKey))
				{
					result.E2EDecryptionKeyType = E2EDecryptionKeyType.DynamicPrivateKey;
				}
				else
				{
					// Possible reasons:
					// - we did not look into the right user's ratchet while trying to determine the sender
					// - The ratchets are not in sync, resend/new dynamic key exchange required
					result.E2EDecryptionKeyType = E2EDecryptionKeyType.UnavailableDynamicPrivateKey;
					return result;
				}
			}

			var dynamicSharedSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(dynamicPrivateKeyOrStaticPrivateKey, dynamicPublicKey);

			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, user.AuthSecret);
			result.E2EDecryptionKeyMaterial = new KeyMaterial64(symmetricKeyMaterial);
			return result;
		}


		public KeyMaterial64 GetInitialE2EDecryptionKey(byte[] dynamicPublicKey)
		{
			var dynamicSharedSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(this._myStaticPrivateKey, dynamicPublicKey);
			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, new byte[32]);
			return new KeyMaterial64(symmetricKeyMaterial);
		}

		public async Task SaveIncomingDynamicPublicKeyOnSuccessfulDecryption(string senderId, byte[] dynamicPublicKey, long dynamicPublicKeyId)
		{
			Guard.NotNull(senderId, dynamicPublicKey);
			if (dynamicPublicKeyId == 0)
				throw new ArgumentException("A dynamic public key must never have an ID of 0.");
			var user = await GetCheckedUser(senderId);
			user.LatestDynamicPublicKey = dynamicPublicKey;
			user.LatestDynamicPublicKeyId = dynamicPublicKeyId;
			await this._updateUser(user);
		}

		public async Task<Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>> GetE2EEncryptionKeyCommon(string recipientId, bool? isInitial)
		{
			E2EUser user = await GetCheckedUser(recipientId);
			if (user.IsJustInitialized)
				isInitial = true;

			if (isInitial == true) // When the contact was just added (the ratchet was just initialized, AuthSecret was null before), or we are answering for a resent request, we use this 'initial' method.
				return await GetInitialEndToEndEncryptionKey(recipientId);
			return await GetEndToEndEncryptionKey(recipientId);
		}

		async Task<Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>> GetEndToEndEncryptionKey(string recipientId)
		{
			E2EUser user = await GetCheckedUser(recipientId);
			long existingMaxKeyId = 0;
			if (user.DynamicPrivateDecryptionKeys.Keys.Count > 0) // count might be 0 initially...might be a bug or not
			{
				existingMaxKeyId = user.DynamicPrivateDecryptionKeys.Keys.Max();
			}

			long nextDynamicPublicKeyId = this._ratchetTimer.GetNextTicks(existingMaxKeyId);

			var ecdhKeypair = this._visualCrypt2Service.GenerateECKeyPair().Result;
			byte[] dynamicPublicKey = ecdhKeypair.PublicKey;

			long privateKeyHint;

			user.DynamicPrivateDecryptionKeys[nextDynamicPublicKeyId] = ecdhKeypair.PrivateKey;
			RemoveExcessKeys(user);
			await this._updateUser(user);

			byte[] dynamicOrStaticPublicKey;
			if (user.LatestDynamicPublicKey != null)
			{
				dynamicOrStaticPublicKey = user.LatestDynamicPublicKey;
				privateKeyHint = user.LatestDynamicPublicKeyId;
			}
			else
			{
				dynamicOrStaticPublicKey = user.StaticPublicKey;
				privateKeyHint = 0;
			}

			var dynamicSharedSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(ecdhKeypair.PrivateKey, dynamicOrStaticPublicKey);

			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, user.AuthSecret);
			return new Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>(new KeyMaterial64(symmetricKeyMaterial), dynamicPublicKey, nextDynamicPublicKeyId, privateKeyHint, null);
		}

		async Task<Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>> GetInitialEndToEndEncryptionKey(string recipientId)
		{
			E2EUser user = await GetCheckedUser(recipientId);

			// user.DynamicPrivateDecryptionKeys = new Dictionary<long, byte[]>(); // don't do this. Or only the last receipt of a resent message can be decrypted
			user.LatestDynamicPublicKey = null;
			user.LatestDynamicPublicKeyId = 0;


			long nextDynamicPublicKeyId = this._ratchetTimer.GetNextTicks(0);
			var ecdhKeypair = this._visualCrypt2Service.GenerateECKeyPair().Result;
			byte[] dynamicPublicKey = ecdhKeypair.PublicKey;

			user.DynamicPrivateDecryptionKeys[nextDynamicPublicKeyId] = ecdhKeypair.PrivateKey;
			RemoveExcessKeys(user);

			await this._updateUser(user);

			long privateKeyHint = 0;

			var dynamicSharedSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(ecdhKeypair.PrivateKey, user.StaticPublicKey);
			var symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, user.AuthSecret);
			var symmetricKeyMaterialMetaData = ByteArrays.Concatenate(dynamicSharedSecret, new byte[32]); // note we are not using user.AuthSecret fro the metadata

			return new Tuple<KeyMaterial64, byte[], long, long, KeyMaterial64>(new KeyMaterial64(symmetricKeyMaterial), dynamicPublicKey, nextDynamicPublicKeyId, privateKeyHint, new KeyMaterial64(symmetricKeyMaterialMetaData));
		}


		// TODO: Review this, compare it with TLSCLient.RemovePreviousKeys and when key cleanup is done
		// This may not work correctly.
		void RemoveExcessKeys(E2EUser user)
		{
			var excess = user.DynamicPrivateDecryptionKeys.Keys.OrderByDescending(k => k).Skip(KeepLatestDynamicPrivateKeys);
			foreach (var keyId in excess)
				user.DynamicPrivateDecryptionKeys.Remove(keyId);
		}

		async Task<E2EUser> GetCheckedUser(string userId)
		{
			E2EUser user = await this._gu(userId);
			if (user.DynamicPrivateDecryptionKeys == null)
				throw new InvalidOperationException("Must be guaranteed on object creation, atm in AppRepository, line 315.");
			if (user.AuthSecret == null)
			{
				user.AuthSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(this._myStaticPrivateKey,
					user.StaticPublicKey);
				user.IsJustInitialized = true;
			}

			if (user.IsJustInitialized)
				await this._updateUser(user);
			return user;
		}

	}
}