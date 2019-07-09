using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.SecureApi.Models;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    public class SecureApiControllerBase : Controller
    {
        protected static string[] CommandsWithoutWalletNameCheck = { };

        protected static ECCModel CreateOk(RequestObject request)
        {
            var responseObject = new ResponseObject<object> { Status = 200, StatusText = "OK" };
            var responseJson = Serialize(responseObject);
            var responseJsonBytes = responseJson.ToUTF8Bytes();
            var cipherV2Bytes = VCL.Encrypt(responseJsonBytes, request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey);
            ECCModel eccModel = new ECCModel { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(), CipherV2Bytes = cipherV2Bytes.ToHexString() };
            return eccModel;
        }

        protected static ECCModel CreateOk<T>(T data, RequestObject request)
        {
            var responseObject = new ResponseObject<T> { ResponsePayload = data, Status = 200, StatusText = "OK" };
            var responseJson = Serialize(responseObject);
            var responseJsonBytes = responseJson.ToUTF8Bytes();
            var cipherV2Bytes = VCL.Encrypt(responseJsonBytes, request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey);
            ECCModel eccModel = new ECCModel { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(), CipherV2Bytes = cipherV2Bytes.ToHexString() };
            return eccModel;
        }

        protected static ECCModel CreateError(Exception e, RequestObject request)
        {
            var responseObject = new ResponseObject<object>();
            if (e is SegWitWalletException se)
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
            var cipherV2Bytes = VCL.Encrypt(responseJsonBytes, request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey);
            ECCModel eccModel = new ECCModel { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(), CipherV2Bytes = cipherV2Bytes.ToHexString() };
            return eccModel;
        }

        protected static ECCModel CreatePublicKey()
        {
            return new ECCModel { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString() };
        }

        protected static DecryptedRequest DecryptRequest(RequestObject request, WalletController walletController)
        {
            byte[] decrypted = VCL.Decrypt(request.CipherV2Bytes.FromBase64(), request.CurrentPublicKey.FromBase64(), VCL.ECKeyPair.PrivateKey);
            if (decrypted == null)
                throw new SegWitWalletException((HttpStatusCode)427, "Public key changed - please reload", null);
            string json = decrypted.FromUTF8Bytes();
            DecryptedRequest decryptedRequest = JsonConvert.DeserializeObject<DecryptedRequest>(json);

            if (Array.IndexOf(CommandsWithoutWalletNameCheck, decryptedRequest.Target) == -1)
                return decryptedRequest;
            walletController.GetManager(decryptedRequest.Target);
            return decryptedRequest;
        }


        protected static bool IsRequestForPublicKey(RequestObject request)
        {
            if (string.IsNullOrEmpty(request.CurrentPublicKey) || string.IsNullOrEmpty(request.CipherV2Bytes))
                return true;
            return false;
        }

        protected static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        static string Serialize<T>(ResponseObject<T> responseObject)
        {
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
            string json = JsonConvert.SerializeObject(responseObject, new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                Formatting = Formatting.Indented
            });
            return json;
        }
    }
}
