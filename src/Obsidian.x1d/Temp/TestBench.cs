using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
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


                //await StartMiningAsync();
                //await SplitAsync();
                await TryStakingAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }

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
                        _logger.LogInformation($"Staking: Enabled: {info.Enabled}, Staking: {info.Staking}.");
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



        static async Task SplitAsync()
        {
            Controller.LoadWallet();

            BuildTransactionResponse model = Controller.BuildSplitTransaction(new BuildTransactionRequest { Passphrase = _passPhrase });
            await Task.Delay(15000); // wait for connections
            Controller.SendTransaction(new Stratis.Bitcoin.Features.Wallet.Models.SendTransactionRequest
            {
                Hex = model.Hex
            });
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
