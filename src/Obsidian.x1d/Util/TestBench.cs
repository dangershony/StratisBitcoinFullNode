using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Tools;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.x1d.Util
{
    public static class TestBench
    {
        static ILogger _logger;
        static FullNode _fullNode;
        static string _walletName = "new1";
        static string _passPhrase = "passwordpassword";

        static WalletController Controller
        {
            get
            {
                var controller = _fullNode.NodeService<WalletController>();
                controller.SetWalletName(_walletName);
                return controller;
            }
        }

        public static async void RunTestCodeAsync(FullNode fullNode)
        {
            var i = 0;
            try
            {
                _logger = fullNode.NodeService<ILoggerFactory>().CreateLogger(typeof(TestBench).FullName);
                _fullNode = fullNode;

                TryCopyWalletForUpdate();
                await LoadOrCreateWalletAsync();

               
                await Task.Delay(10000);
                for (i = 0; i < 1; i++)
                {
                    //await Send(100 * Satoshi.Long, "odx1qjdldsm72vr4tmlecjfstr5clkk6pzux47f3e8x"); // buta
                    //await Send(1_000_000 * Satoshi.Long, "odx1qynfypr709q086jhjjxc324aqcwtvcpym0crm6c"); // blacky #2
                    //await Task.Delay(1000);
                }

                await TryStakingAsync();
            }
            catch (Exception e)
            {
                _logger.LogError($"Error at {i}:{e}");
            }

        }

        static async Task LoadOrCreateWalletAsync()
        {
            try
            {
                Controller.LoadWallet();
                _logger.LogInformation($"Loaded wallet '{_walletName}'.");
            }
            catch (Exception e)
            {
                _logger.LogWarning(e.Message);

                if (!e.Message.StartsWith("No wallet file found"))
                    throw;

                Controller.CreateWallet(new WalletCreateRequest
                { WalletName = _walletName, Passphrase = _passPhrase });

                _logger.LogInformation($"Created a new wallet named '{_walletName}'.");
                await Task.Delay(2000);
                await LoadOrCreateWalletAsync();
            }
        }

        static void TryCopyWalletForUpdate()
        {
            var currentWalletPath = _fullNode.NodeService<DataFolder>().WalletPath;
            var currentSegments = currentWalletPath.Split(Path.DirectorySeparatorChar);
            var oldSegments = new List<string>();
            bool found = false;
            foreach (var seg in currentSegments)
            {
                if (!found && (seg == ".obsidian" || seg == "Obsidian"))
                {
                    if (seg == ".obsidian")
                        oldSegments.Add(".stratisnode");
                    else
                        oldSegments.Add("StratisNode");
                    found = true;
                }
                else
                {
                    oldSegments.Add(seg);
                }
            }

            var oldWalletDirPath = string.Join(Path.DirectorySeparatorChar, oldSegments);
            var oldWalletPath = Path.Combine(oldWalletDirPath, "new1.ODX.x1wallet.json");
            var newWalletPath = Path.Combine(currentWalletPath, "new1.ODX.x1wallet.json");
            if (!File.Exists(newWalletPath))
                if (File.Exists(oldWalletPath))
                    File.Copy(oldWalletPath, newWalletPath);
        }

        static async Task Send(long satoshis, string address)
        {

            var recipients = new List<Recipient> { new Recipient { Amount = satoshis, Address = address } };
            var tx = Controller.BuildTransaction(new TransactionRequest
            { Recipients = recipients, Passphrase = _passPhrase, Sign = true, Send = true });
        }

        static async Task TryStakingAsync()
        {
            try
            {
                _logger.LogInformation("Starting staking...");
                Controller.StartStaking(new StartStakingRequest { Passphrase = _passPhrase });
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }




        static async Task SplitAsync()
        {


            TransactionResponse model = Controller.BuildSplitTransaction(new TransactionRequest { Passphrase = _passPhrase, Sign = true, Send = true });
        }

        static async Task StartMiningAsync()
        {

            var ibd = _fullNode.NodeService<IInitialBlockDownloadState>();
            try
            {
                Controller.LoadWallet();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (!e.Message.StartsWith("No wallet file found"))
                    throw;
                Controller.CreateWallet(new WalletCreateRequest
                { WalletName = _walletName, Passphrase = _passPhrase });
                Console.WriteLine($"Created a new wallet {_walletName} for mining.");

                await StartMiningAsync();

            }
            await Task.Delay(10000);
            var model = Controller.GetUnusedReceiveAddresses();
            var address = model.Addresses[0].FullAddress;

            var script = new ReserveScript { ReserveFullNodeScript = address.ScriptPubKeyFromPublicKey() };
            _ = Task.Run(() =>
            {
                _logger.LogInformation("Starting Miner...");

                while (!_fullNode.NodeLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    _fullNode.NodeService<IPowMining>().GenerateBlocks(script, 1, 1000 * 1000);
                    _logger.LogInformation("Mining...");
                }
            }, _fullNode.NodeLifetime.ApplicationStopping);
        }
    }
}
