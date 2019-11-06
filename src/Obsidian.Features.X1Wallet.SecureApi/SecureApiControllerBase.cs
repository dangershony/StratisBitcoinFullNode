using System;
using System.Collections;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.SecureApi.Models;
using Obsidian.Features.X1Wallet.Tools;
using VisualCrypt.VisualCryptLight;
using VisualCrypt.VisualCryptLight.VisualCryptLib.ECC;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    public class SecureApiControllerBase : Controller
    {
        internal static ECKeyPair AuthKey { get; set; }

        protected static string[] CommandsWithoutWalletNameCheck = { };

        protected static ECCModel CreateOk(RequestObject request)
        {
            if (AuthKey == null)
                throw new X1WalletException(HttpStatusCode.NoContent, "Please retry later.", null);

            var responseObject = new ResponseObject<object> { Status = 200, StatusText = "OK" };
            var responseJson = Serialize(responseObject);
            var responseJsonBytes = responseJson.ToUTF8Bytes();
            var cipherV2Bytes = VCL.Encrypt(responseJsonBytes, request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey, AuthKey.PrivateKey);
            ECCModel eccModel = new ECCModel
            {
                CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(),
                CipherV2Bytes = cipherV2Bytes.ToHexString(),
                AuthKey = AuthKey.PublicKey.ToHexString()
            };
            return eccModel;
        }

        protected static ECCModel CreateOk<T>(T data, RequestObject request)
        {
            if (AuthKey == null)
                throw new X1WalletException(HttpStatusCode.NoContent, "Please retry later.", null);

            var responseObject = new ResponseObject<T> { ResponsePayload = data, Status = 200, StatusText = "OK" };
            var responseJson = Serialize(responseObject);
            var responseJsonBytes = responseJson.ToUTF8Bytes();
            var cipherV2Bytes = VCL.Encrypt(responseJsonBytes, request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey, AuthKey.PrivateKey);
            ECCModel eccModel = new ECCModel
            {
                CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(),
                CipherV2Bytes = cipherV2Bytes.ToHexString(),
                AuthKey = AuthKey.PublicKey.ToHexString()
            };
            return eccModel;
        }

        protected static ECCModel CreateError(Exception e, RequestObject request)
        {
            if (AuthKey == null)
                throw new X1WalletException(HttpStatusCode.NoContent, "Please retry later.", null);

            var responseObject = new ResponseObject<object>();
            if (e is X1WalletException se)
            {
                responseObject.Status = (int)se.HttpStatusCode;
                responseObject.StatusText = se.Message;
            }
            else
            {
                responseObject.Status = 500;
                responseObject.StatusText = $"Error: {e.Message}";
            }
            var responseJson = Serialize(responseObject);
            var responseJsonBytes = responseJson.ToUTF8Bytes();
            var cipherV2Bytes = VCL.Encrypt(responseJsonBytes, request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey, AuthKey.PrivateKey);
            ECCModel eccModel = new ECCModel
            {
                CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(),
                CipherV2Bytes = cipherV2Bytes.ToHexString(),
                AuthKey = AuthKey.PublicKey.ToHexString()
            };
            return eccModel;
        }

        protected static ECCModel CreatePublicKey()
        {
            if (AuthKey == null)
                throw new X1WalletException(HttpStatusCode.NoContent, "Please retry later.", null);

            return new ECCModel
            {
                CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(),
                AuthKey = AuthKey.PublicKey.ToHexString()
            };
        }

        protected static DecryptedRequest DecryptRequest(RequestObject request, WalletController walletController)
        {
            byte[] decrypted = VCL.Decrypt(request.CipherV2Bytes.FromBase64(), request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey, AuthKey.PrivateKey);
            if (decrypted == null)
                throw new X1WalletException((HttpStatusCode)427, "Public key changed - please reload", null);
            string json = decrypted.FromUTF8Bytes();
            DecryptedRequest decryptedRequest = JsonConvert.DeserializeObject<DecryptedRequest>(json);

            if (((IList)CommandsWithoutWalletNameCheck).Contains(decryptedRequest.Command))
                return decryptedRequest;
            if (decryptedRequest.Target == null)
                throw new X1WalletException(HttpStatusCode.BadRequest, "No wallet name was supplied.");
            walletController.SetWalletName(decryptedRequest.Target.Replace($".{walletController.CoinTicker}{X1WalletFile.FileExtension}", ""));
            return decryptedRequest;
        }


        protected static bool IsRequestForPublicKey(RequestObject request)
        {
            if (string.IsNullOrEmpty(request.CurrentPublicKey) || string.IsNullOrEmpty(request.CipherV2Bytes))
                return true;
            return false;
        }

        protected static void CheckPermissions(DecryptedRequest decryptedRequest, SecureApiSettings secureApiSettings)
        {
            if (decryptedRequest == null)
                throw new ArgumentNullException(nameof(decryptedRequest));

            if (!secureApiSettings.IsSecureApiEnabled)
                throw new X1WalletException(HttpStatusCode.Unauthorized,
                    "SecureApi is disabled by configuration/arguments", null);

            if (string.IsNullOrWhiteSpace(secureApiSettings.SecureApiUser) || string.IsNullOrWhiteSpace(secureApiSettings.SecureApiPassword))
                return;

            var user = secureApiSettings.SecureApiUser.Trim();
            var password = secureApiSettings.SecureApiPassword.Trim();
            if (user != decryptedRequest.User || password != decryptedRequest.Password)
                throw new X1WalletException(HttpStatusCode.Unauthorized,
                    "Invalid credentials.", null);

        }

        protected static T Deserialize<T>(string json)
        {
            return Serializer.Deserialize<T>(json);
        }

        static string Serialize<T>(ResponseObject<T> responseObject)
        {
            return Serializer.Serialize(responseObject);
        }
    }
}
