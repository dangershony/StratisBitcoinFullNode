using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Implementations;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces;
using VisualCrypt.VisualCryptLight.VisualCryptLib.E2E;
using VisualCrypt.VisualCryptLight.VisualCryptLib.ECC;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.TLS
{
    public class TLSServerRatchet
    {
        readonly Dictionary<long, string> _idsByPrivateKeyHint = new Dictionary<long, string>();
        readonly Dictionary<string, TLSUser> _usersById = new Dictionary<string, TLSUser>();
        readonly RatchetTimer _ratchetTimer = new RatchetTimer();

        /// <summary>
        /// Do not change this! Must be 2. We must push 2 new dynamic secrets before reusing a dynamic secret, 
        /// because otherwise we will not yet be using the other one's dynamic public key.
        /// </summary>
        const int RequiredPushesBeforeReUseIsAllowed = 2;
        const int TimesToReUseDynamicSecretAfterEstablishment = 5;
        const int KeepLatestDynamicPrivateKeys = 50;

        readonly IVisualCrypt2Service _visualCrypt2Service;

        readonly byte[] _serverPrivateKey;
       

        public TLSServerRatchet(string serverId, byte[] serverPrivateKey, IVisualCrypt2Service visualCrypt2Service)
        {
            this.ServerId = serverId;
            this._serverPrivateKey = serverPrivateKey;
            this._visualCrypt2Service = visualCrypt2Service;
           
        }

        public string ServerId { get; }

        public void RefreshTLSUser(string userId, byte[] staticPublicKey)
        {
            Guard.NotNull(userId, staticPublicKey);
            var user = new TLSUser(userId, staticPublicKey);
            this._usersById[userId] = user;
        }

        public TLSEnvelope TLSServerEncryptRequest(byte[] clearPacket, string recipientId)
        {
            byte[] authSecret = GetAuthSecret(recipientId);

            DynamicSecret dynamicSecret = GetDynamicSecretForEncryption(recipientId);

            Debug.WriteLine($"{this.ServerId}: TLSEncrypt: DynamicPublicKeyID: {dynamicSecret.DynamicPublicKeyId}, PrivateKeyHint: {dynamicSecret.PrivateKeyHint}.");

            // Concatenate = 'TLSAuthMode.Combined'
            byte[] symmetricKeyMaterial64 = ByteArrays.Concatenate(dynamicSecret.DynamicSharedSecret, authSecret);

            var lro = new LongRunningOperation(progress => { }, () => { });
            var clearBytes = new Clearbytes(clearPacket);
            var sha512PW64 = new KeyMaterial64(symmetricKeyMaterial64);
            var method = new RoundsExponent(0xff);

            var encryptResponse = this._visualCrypt2Service.BinaryEncrypt(clearBytes, sha512PW64, method, lro.Context);
            if (!encryptResponse.IsSuccess)
                throw new Exception(encryptResponse.Error);

            var encodeResponse = this._visualCrypt2Service.BinaryEncodeVisualCrypt(encryptResponse.Result, lro.Context);
            if (!encodeResponse.IsSuccess)
                throw new Exception(encodeResponse.Error);

            var tlsEnvelope = new TLSEnvelope(dynamicSecret.PrivateKeyHint, dynamicSecret.DynamicPublicKeyId, dynamicSecret.DynamicPublicKey, encodeResponse.Result);
            return tlsEnvelope;
        }

        public TLSEnvelope TLSServerEncryptRequestAnonymous(byte[] clearPacket, byte[] dynamicPublicKey, long dynamicPublicKeyID)
        {

            byte[] authSecret = new byte[32]; // we cannot create an authSecret based on the client's public key when we don't know who the client is.

            // we use the dynamicPublicKey and the server private key.
            var dynamicSharedSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(this._serverPrivateKey, dynamicPublicKey);

            // the hint to the clients privk for pubkey he sent
            long privateKeyHint = dynamicPublicKeyID;

            // and now we create a dynamic public key, just to fit the protocol, but not intended for use.
            var throwAwayPubKey = this._visualCrypt2Service.GenerateECKeyPair().Result.PublicKey;
            // and a fake id
            long fakeDynamicPublicKeyID = 9999; // use a realistic value, not 9999!
            Debug.WriteLine($"{this.ServerId}: TLSServerEncryptRequestAnonymous: FakeDynamicPublicKeyID: {fakeDynamicPublicKeyID}, PrivateKeyHint: {privateKeyHint}.");

            // Concatenate = 'TLSAuthMode.Dynamic' - THIS is anothe case!
            byte[] symmetricKeyMaterial64 = ByteArrays.Concatenate(dynamicSharedSecret, authSecret);

            // same as normally
            var lro = new LongRunningOperation(progress => { }, () => { });
            var clearBytes = new Clearbytes(clearPacket);
            var sha512PW64 = new KeyMaterial64(symmetricKeyMaterial64);
            var method = new RoundsExponent(0xff);

            var encryptResponse = this._visualCrypt2Service.BinaryEncrypt(clearBytes, sha512PW64, method, lro.Context);
            if (!encryptResponse.IsSuccess)
                throw new Exception(encryptResponse.Error);

            var encodeResponse = this._visualCrypt2Service.BinaryEncodeVisualCrypt(encryptResponse.Result, lro.Context);
            if (!encodeResponse.IsSuccess)
                throw new Exception(encodeResponse.Error);

            var tlsEnvelope = new TLSEnvelope(privateKeyHint, fakeDynamicPublicKeyID, throwAwayPubKey, encodeResponse.Result);
            return tlsEnvelope;
        }

        public TLSRequest TLSServerDecryptRequest(TLSEnvelope tlsEnvelope)
        {

            Guard.NotNull(tlsEnvelope);
            var ar = new TLSRequest();

            byte[] serverPrivateKey; // static or dynamic
            if (tlsEnvelope.PrivateKeyHint == 0) // The client has used server's static public key to encrypt...
                serverPrivateKey = this._serverPrivateKey;
            // ... so we simple use the servers static private key
            else // tlsEnvelope.PrivateKeyHint is the number the server generated earlier
                serverPrivateKey = GetServerDynamicPrivateKeyForUserByPrivateKeyHint(tlsEnvelope.PrivateKeyHint);
            if (serverPrivateKey == null)
            {
                // we lost our dynamic private key b/c of a disk failure and we don't know who sent us what :-(
                return null;
            }

            byte[] dynamicSharedSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(serverPrivateKey, tlsEnvelope.DynamicPublicKey);

            Debug.WriteLine($"{this.ServerId}: TLSDecrypt:  PrivateKeyHint: {tlsEnvelope.PrivateKeyHint}");

            // 'TLSAuthMode.Separate'
            byte[] symmetricKeyMaterial = ByteArrays.Concatenate(dynamicSharedSecret, new byte[32]); // no auth, we dont know who the user is
            // End 'TLSAuthMode.Separate'

            var lro = new LongRunningOperation(progress => { }, () => { });
            var cipherV2 = VisualCrypt2Formatter.DissectVisualCryptBytes(tlsEnvelope.EncipheredPayload, lro.Context);
            var decryptResponse = this._visualCrypt2Service.BinaryDecrypt(cipherV2, new KeyMaterial64(symmetricKeyMaterial), lro.Context);
            if (!decryptResponse.IsSuccess)
                throw new Exception(decryptResponse.Error);

            byte[] tlsDecryptedRequest = decryptResponse.Result.GetBytes();

            // 'TLSAuthMode.Separate'
            var authSecretFromMessage = new byte[32];
            Buffer.BlockCopy(tlsDecryptedRequest, 0, authSecretFromMessage, 0, authSecretFromMessage.Length);

            // must be set even if the user is not authenticated for the case where a new identity is published right now.
            ar.UserId = Encoding.UTF8.GetString(tlsDecryptedRequest, 32, 10);
            // try authenticate
            var authSecret = GetAuthSecret(ar.UserId);
            if (authSecret != null)
            {
                if (ByteArrays.AreAllBytesEqual(authSecretFromMessage, authSecret))
                {
                    ar.IsAuthenticated = true;
                }
            }
            else
                ar.IsAuthenticated = false;

            var requestData = new byte[tlsDecryptedRequest.Length - 32 - 10];
            Buffer.BlockCopy(tlsDecryptedRequest, 32 + 10, requestData, 0, requestData.Length);
            ar.CommandData = requestData;
            // End 'TLSAuthMode.Separate'

            if (ar.IsAuthenticated)
                SaveIncomingDynamicPublicKey(ar.UserId, tlsEnvelope.DynamicPublicKey, tlsEnvelope.DynamicPublicKeyId);

            return ar;
        }

        TLSUser GetUser(string userId)
        {
            if (this._usersById.ContainsKey(userId))
                return this._usersById[userId];
            return null;
        }


        public void SaveIncomingDynamicPublicKey(string userId, byte[] dynamicPublicKey, long dynamicPublicKeyId)
        {
            Guard.NotNull(userId, dynamicPublicKey);
            if (dynamicPublicKeyId == 0)
                throw new ArgumentException("A dynamic public key must never have an ID of 0.");
            var user = GetUser(userId);
            if (user == null)
                throw new Exception("Users must be known before we save dynamic public keys;");
            user.LatestDynamicPublicKey = dynamicPublicKey;
            user.LatestDynamicPublicKeyId = dynamicPublicKeyId;
        }

        byte[] GetAuthSecret(string userId)
        {
            Guard.NotNull(userId);
            var user = GetUser(userId);
            if (user == null)
                return null;
            if (user.AuthSecret != null)
                return user.AuthSecret;

            var staticPublicKey = user.StaticPublicKey;
            if (staticPublicKey == null)
                throw new Exception("When the user was found, its public key must not be null.");

            user.AuthSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(this._serverPrivateKey, staticPublicKey);
            return user.AuthSecret;
        }

        /// <summary>
        /// Gets the new or reused dynamic secret for TLS encryption for a client.
        /// </summary>
        /// <param name="userId">The Id of the client.</param>
        /// <returns>The dynamic encryption secret.</returns>
        DynamicSecret GetDynamicSecretForEncryption(string userId)
        {
            Guard.NotNull(userId);
            var user = GetUser(userId);
            if (user == null)
                return null;

            if (user.DynamicSecret == null)
            {
                NewDynamicEncryptionSecret(user);
                user.PushesDone++;
                Debug.WriteLine($"{this.ServerId} *** Created a new dynamic secret for encryption for client {userId}, because there is none for reuse.");
            }
            else if (user.PushesDone < RequiredPushesBeforeReUseIsAllowed)
            {
                NewDynamicEncryptionSecret(user);
                user.PushesDone++;
                Debug.WriteLine($"{this.ServerId} *** Created a new dynamic secret for encryption for client {userId}, because key reuse is not yet allowed");
            }
            else if (user.DynamicSecret.UseCount >= TimesToReUseDynamicSecretAfterEstablishment)
            {
                NewDynamicEncryptionSecret(user);
                user.PushesDone++;
                Debug.WriteLine($"{this.ServerId} *** Created a new dynamic secret for encryption for client {userId}, because time was up.");
            }
            Debug.Assert(user.DynamicSecret != null);
            user.DynamicSecret.UseCount++;
            Debug.WriteLine($"{this.ServerId} *** Used same dynamic secret for client {userId} {user.DynamicSecret.UseCount} times. Pushed {user.PushesDone} dynamic secrets in total.");
            return user.DynamicSecret;
        }


        void NewDynamicEncryptionSecret(TLSUser user)
        {
            var newKeyPair = this._visualCrypt2Service.GenerateECKeyPair().Result;
            var newDynamicKeyId = this._ratchetTimer.GetNextTicks(user.DynamicPrivateDecryptionKeys.Count > 0
                   ? user.DynamicPrivateDecryptionKeys.Keys.Max()
                   : 0);

            Debug.Assert(user.LatestDynamicPublicKey != null && user.LatestDynamicPublicKeyId != 0,
                "The client always sends a dynamic public key, so we must have it.");

            var newDynamicSecret = this._visualCrypt2Service.CalculateAndHashSharedSecret(newKeyPair.PrivateKey, user.LatestDynamicPublicKey);
            user.DynamicSecret = new DynamicSecret(recipientId: null,
                dynamicSharedSecret: newDynamicSecret,
                dynamicPublicKey: newKeyPair.PublicKey,
                dynamicPublicKeyId: newDynamicKeyId,
                privateKeyHint: user.LatestDynamicPublicKeyId)
            { UseCount = 0 };

            user.DynamicPrivateDecryptionKeys[newDynamicKeyId] = newKeyPair.PrivateKey;
            this._idsByPrivateKeyHint[newDynamicKeyId] = user.UserId;

            RemoveExcessKeys(user);

        }

        // TODO: Review this, compare it with TLSCLient.RemovePreviousKeys and when key cleanup is done
        // This may not work correctly.
        void RemoveExcessKeys(TLSUser user)
        {
            var excess = user.DynamicPrivateDecryptionKeys.Keys.OrderByDescending(k => k).Skip(KeepLatestDynamicPrivateKeys);
            foreach (var keyId in excess)
            {
                user.DynamicPrivateDecryptionKeys.Remove(keyId);
                this._idsByPrivateKeyHint.Remove(keyId);
            }
        }


        /// <summary>
        /// Gets the dynamic decryption secret, using the supplied public key and the dynamic private key
        /// that is hinted at.
        /// If the privateKeyHint is 0, the server private Auth key is used.
        /// If the dynamic private key from the hint has been lost, null is returned.
        /// The calculation of the secret could be cached.
        /// </summary>
        byte[] GetServerDynamicPrivateKeyForUserByPrivateKeyHint(long privateKeyHint)
        {
            Debug.Assert(privateKeyHint != 0); // the caller has already checked that we don't look for the server's static private key

            // The client has used a previously generated dynamic public key from the server, 
            // and now the private key for that key is needed. privateKeyHint points to that key.
            string userId;
            if (!this._idsByPrivateKeyHint.TryGetValue(privateKeyHint, out userId))
                return null;// The key is lost! Disk failure. No worries, we handle that!
            var user = GetUser(userId);
            return user.DynamicPrivateDecryptionKeys[privateKeyHint];
        }
    }
}