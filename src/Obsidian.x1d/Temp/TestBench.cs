using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Tools;
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

        static WalletController WC
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

               
                await  StartMiningAsync();
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
            await WC.LoadAsync();

            while (!_fullNode.NodeService<INodeLifetime>().ApplicationStopping.IsCancellationRequested)
            {
                try
                {
                    var info = WC.GetStakingInfo();
                    if (info != null && info.Enabled)
                        _logger.LogInformation($"Staking: Enabled: {info.Enabled}, Staking: {info.Staking}.");
                    else
                    {
                        _logger.LogInformation($"Staking: Trying to start staking....");
                        await WC.StartStaking(new Features.X1Wallet.Staking.StartStakingRequest
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
            await WC.LoadAsync();

            var changeAddress = await WC.GetUnusedReceiveAddresses();
            var model = await WC.BuildSplitTransactionAsync(
                new Stratis.Bitcoin.Features.Wallet.Models.BuildTransactionRequest
                {
                    Password =_passPhrase,
                    ChangeAddress = changeAddress.Addresses.First().Address,

                });
            await Task.Delay(15000); // wait for connections
            await WC.SendTransactionAsync(new Stratis.Bitcoin.Features.Wallet.Models.SendTransactionRequest
            {
                Hex = model.Hex
            });
        }

        static async Task StartMiningAsync()
        {

            var ibd = _fullNode.NodeService<IInitialBlockDownloadState>();
            try
            {
                await WC.LoadAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (!e.Message.StartsWith("No wallet file found"))
                    throw;
                await WC.CreateKeyWalletAsync(new WalletCreateRequest
                { Name = _walletName, Password = _passPhrase });
                Console.WriteLine($"Created a new wallet {_walletName} for mining.");
                await Task.Delay(2000);
                await StartMiningAsync();

            }

            var model = await WC.GetUnusedReceiveAddresses();
            var address = model.Addresses[0].FullAddress;

            var script = new ReserveScript { ReserveFullNodeScript = address.ScriptPubKeyFromPublicKey() };
            _ = Task.Run(async () =>
            {
                while (ibd.IsInitialBlockDownload())
                    await Task.Delay(1000);

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
