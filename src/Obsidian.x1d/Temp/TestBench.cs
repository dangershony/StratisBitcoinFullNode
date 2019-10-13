using System;
using System.Threading.Tasks;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.Storage;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;

namespace Obsidian.x1d.Temp
{
    public static class TestBench
    {

        public static async void RunTestCodeAsync(FullNode fullNode)
        {
            try
            {
               await  MineAsync(fullNode);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        static async Task MineAsync(FullNode fullNode)
        {
            const string walletName = "new1";

            var controller = fullNode.NodeService<WalletController>();
            try
            {
                controller.SetWalletName(walletName);
                await controller.LoadAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (!e.Message.StartsWith("No wallet file found"))
                    throw;
                await controller.CreateKeyWalletAsync(new Features.X1Wallet.Models.WalletCreateRequest
                { Name = walletName, Password = "passwordpassword" });
                Console.WriteLine($"Created a new wallet {walletName} for mining.");
                await MineAsync(fullNode);

            }

            var model = await controller.GetUnusedReceiveAddresses();
            var address = model.Addresses[0].FullAddress;

            var script = new ReserveScript { ReserveFullNodeScript = address.ScriptPubKeyFromPublicKey() };
            _ = Task.Run(async () => 
            {
                while (!fullNode.NodeLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    Console.WriteLine("Starting Miner...");
                    fullNode.NodeService<IPowMining>().GenerateBlocks(script, 50, uint.MaxValue);
                    await Task.Delay(1000);
                  
                }
            }, fullNode.NodeLifetime.ApplicationStopping);
        }
    }
}
