using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Staking;
using Obsidian.Features.X1Wallet.Tools;
using Obsidian.Features.X1Wallet.Transactions;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.x1d.Temp
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
            try
            {
                _logger = fullNode.NodeService<ILoggerFactory>().CreateLogger("Miner");
                _fullNode = fullNode;


                await StartMiningAsync();
                //await Task.Delay(1000 * 120);
                //await SplitAsync();
                //await Send(Money.Coins(1000), "odx1q0693fqjqze4h7jy44vpmp8qtpk8v2rws0xa486");
                await TryStakingAsync();

            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }

        }

        static async Task Send(long satoshis, string address)
        {
            Controller.LoadWallet();
            var recipients = new List<Recipient> { new Recipient { Amount = satoshis, Address = address } };
            var tx = Controller.BuildTransaction(new BuildTransactionRequest
            { Recipients = recipients, Passphrase = _passPhrase, Sign = true, Send = true });
        }

        static async Task TryStakingAsync()
        {
            Controller.LoadWallet();

            while (!_fullNode.NodeService<INodeLifetime>().ApplicationStopping.IsCancellationRequested)
            {
                try
                {
                    var info = Controller.GetStakingInfo();
                    if (info != null && info.Enabled)
                    {
                        _logger.LogInformation($"Staking: Enabled: {info.Enabled}, Expected Time: {info.ExpectedTime}.");
                        Print(info);
                    }
                    else
                    {
                        _logger.LogInformation($"Staking: Trying to start staking....");
                        Controller.StartStaking(new Features.X1Wallet.Staking.StartStakingRequest
                        { Name = _walletName, Password = _passPhrase });
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                }
                await Task.Delay(15000);
            }
        }

        private static void Print(StakingInfo info)
        {
            var json = JsonConvert.SerializeObject(info, Formatting.Indented);
            _logger.LogInformation(json);
        }

        static async Task SplitAsync()
        {
            Controller.LoadWallet();

            BuildTransactionResponse model = Controller.BuildSplitTransaction(new BuildTransactionRequest { Passphrase = _passPhrase, Send = true });
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
                { Name = _walletName, Password = _passPhrase });
                Console.WriteLine($"Created a new wallet {_walletName} for mining.");
                await Task.Delay(2000);
                await StartMiningAsync();

            }

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
