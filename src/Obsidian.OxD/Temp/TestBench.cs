using System;
using NBitcoin;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.Temp;
using Obsidian.Networks.ObsidianX;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Obsidian.OxD.Temp
{
    public static class TestBench
    {
        static BitcoinSecret _minerSecret;
        static HdAddress _hdDddress;

        public static async void Run(FullNode fullNode)
        {


            //CreateNDWallet(fullNode);
            //CreateWalletAndPrintWords(fullNode);


            Miner.Start(fullNode);

            //var account = fullNode.NodeService<IWalletManager>().GetWallet("blackstone").GetAccounts().First();
            //account.CreateAddresses(fullNode.Network, 2);

            //var tx = StaticWallet.CreateTx();

            //StaticWallet.SendTx(tx);


            //await Task.Delay(15000);





            // StaticWallet.PrintBlocks();
        }

        


        static void CreateNDWallet(FullNode fullNode)
        {
            //var segWitWalletController =  fullNode.NodeService<WalletController>();
            //segWitWalletController.CreateKeyWalletAsync("blackstone-nd", "password");
        }

        static void CreateWalletAndPrintWords(FullNode fullNode)
        {
            Mnemonic mnemonic = fullNode.NodeService<IWalletManager>().CreateWallet("password", "blackstone", "passphrase");
            foreach (var w in mnemonic.Words)
                Console.WriteLine(w);
        }

        static Mnemonic CreateMnemonic()
        {
            return new Mnemonic("boss ability moment scissors oven episode head siege void identify photo fabric");
        }



    }
}
//static void Spend3(FullNode fullNode)
//{
//    string walletName = "blackstone";
//    string accountName = "account 0";
//    string walletPassword = "fhdsjkfhjksdlhfjkdlshfkdshfk";

//    WalletAccountReference walletAccountReference = new WalletAccountReference(walletName, accountName);
//    BitcoinAddress destinationAddress = fullNode.WalletManager().GetUnusedAddress(walletAccountReference).Pubkey.WitHash.GetAddress(fullNode.Network);
//    Console.WriteLine($"Attempting to send money to own address{destinationAddress}");

//    Wallet wallet = fullNode.WalletManager().GetWalletByName(walletName);
//    HdAddress sourceAddress = wallet.GetAllAddresses().First();
//    Key k = wallet.GetExtendedPrivateKeyForAddress(walletPassword, sourceAddress).PrivateKey;
//    //This gives you a Bech32 address (currently not really interoperable in wallets, so you need to convert it into P2SH)
//    BitcoinAddress address = k.PubKey.WitHash.GetAddress(fullNode.Network);
//    // BitcoinScriptAddress p2sh = address.GetScriptAddress();


//    var sptxs = fullNode.WalletManager().GetSpendableTransactionsInAccount(walletAccountReference);
//    uint256[] txids = sptxs.Select(s => s.Transaction.Id).ToArray();
//    var transactions = new List<Transaction>();
//    foreach (var id in txids)
//        transactions.Add(fullNode.BlockStore().GetTransactionById(id));
//    var coinList = new List<Coin>();
//    foreach (var tx in transactions)
//    {
//        coinList.Add(new Coin(tx, 0).ToScriptCoin(k.PubKey.WitHash.ScriptPubKey));
//    }

//    Coin[] coins = coinList.ToArray();


//    var builder = new TransactionBuilder(fullNode.Network);
//    builder.AddCoins(coins);
//    builder.AddKeys(k);
//    builder.Send(destinationAddress, Money.Coins(1));
//    builder.SendFees(Money.Coins(0.001m));
//    builder.SetChange(address);
//    Transaction signedTx = builder.BuildTransaction(true);
//    bool success = builder.Verify(signedTx);
//    fullNode.BroadcasterManager().BroadcastTransactionAsync(signedTx).GetAwaiter().GetResult();
//    uint256 hash = signedTx.GetHash();
//    var hashString = hash.ToString();
//    Console.WriteLine(hashString);

//}



//async static void Spend(FullNode fullNode)
//{
//    BitcoinAddress destinationAddress = new Key().PubKey.WitHash.GetAddress(fullNode.Network);  // random dest address
//    var txHash = new uint256("dd175f7979899999d55c0ab9a623bf81cbeda00305a0ec7864ea9aa79c89aa2e");

//    var tx = fullNode.Network.CreateTransaction(
//        "010000009c15f45c010000000000000000000000000000000000000000000000000000000000000000ffffffff04028d0000ffffffff0100ca9a3b00000000220020477308c5535233ce353279d73abfddf39451511ef5da15dec635bf573b968eff00000000");

//    var coin = new Coin(tx, 0);

//    string walletName = "blackstone";
//    string accountName = "account 0";
//    string walletPassword = "fhdsjkfhjksdlhfjkdlshfkdshfk";

//    Wallet wallet = fullNode.WalletManager().GetWalletByName(walletName);
//    var adresses = wallet.GetAllAddresses().ToArray();

//    HdAddress sourceHdAdr = null;
//    foreach (var a in adresses)
//    {
//        sourceHdAdr = a;
//        break;
//    }
//    Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, adresses[0]).PrivateKey;

//    Script redeemScript = adresses[0].Pubkey.WitHash.ScriptPubKey.PaymentScript;

//    TransactionBuilder builder = new TransactionBuilder(fullNode.Network);
//    builder.AddCoins(new[] { coin.ToScriptCoin(redeemScript) });
//    builder.AddKeys(new[] { extendedPrivateKey });
//    builder.Send(destinationAddress, Money.Coins(1));
//    builder.SendFees(Money.Coins(0.001m));
//    builder.SetChange(sourceHdAdr.Pubkey.WitHash.GetAddress(fullNode.Network));
//    var signedTx = builder.BuildTransaction(true);
//    bool success = builder.Verify(signedTx);
//    fullNode.BroadcasterManager().BroadcastTransactionAsync(signedTx).GetAwaiter().GetResult();
//    uint256 hash = signedTx.GetHash();
//    var hashString = hash.ToString();
//}

//async static void Spend2(FullNode fullNode)
//{
//    string walletName = "blackstone";
//    string accountName = "account 0";



//    WalletAccountReference walletAccountReference = new WalletAccountReference(walletName, accountName);


//    BitcoinAddress destinationAddress = fullNode.WalletManager().GetUnusedAddress(walletAccountReference).Pubkey.WitHash.GetAddress(fullNode.Network);
//    Console.WriteLine($"Attempting to send money to own address{destinationAddress}");
//    //UnspentOutputReference utxo = fullNode.WalletManager().GetSpendableTransactionsInAccount(walletAccountReference, 500).First();

//    var sptxs = fullNode.WalletManager().GetSpendableTransactionsInAccount(walletAccountReference);
//    uint256[] txids = sptxs.Select(s => s.Transaction.Id).ToArray();
//    var transactions = new List<Transaction>();
//    foreach (var id in txids)
//        transactions.Add(fullNode.BlockStore().GetTransactionById(id));
//    var coins = new List<Coin>();
//    foreach (var tx in transactions)
//        coins.Add(new Coin(tx, 0));

//    //var txHash = new uint256("dd175f7979899999d55c0ab9a623bf81cbeda00305a0ec7864ea9aa79c89aa2e");

//    //var tx = fullNode.Network.CreateTransaction(
//    //    "010000002f7bf15c010000000000000000000000000000000000000000000000000000000000000000ffffffff025900ffffffff010084d717000000002200208c3ea422668a241d34abdb9e826e66b4caea737e9fcd45ab0226125d312662f100000000");


//    //var adresses = fullNode.WalletManager().GetAccounts("blackstone").First().FindAddressesForTransaction((d) => true);




//    string walletPassword = "fhdsjkfhjksdlhfjkdlshfkdshfk";

//    Wallet wallet = fullNode.WalletManager().GetWalletByName(walletName);
//    var adresses = wallet.GetAllAddresses().ToArray();





//    TransactionBuilder builder = new TransactionBuilder(fullNode.Network);
//    builder.AddCoins(coins.Select(c =>
//    {

//        var sc = c.ToScriptCoin(c.ScriptPubKey.WitHash.ScriptPubKey);
//        return sc;

//    }));
//    builder.AddKeys(adresses.Select(a => wallet.GetExtendedPrivateKeyForAddress(walletPassword, a).PrivateKey).ToArray());
//    builder.Send(destinationAddress, Money.Coins(1));
//    builder.SendFees(Money.Coins(0.001m));
//    builder.SetChange(adresses[0].Pubkey.WitHash.GetAddress(fullNode.Network));
//    var signedTx = builder.BuildTransaction(true);
//    bool success = builder.Verify(signedTx);
//    fullNode.BroadcasterManager().BroadcastTransactionAsync(signedTx).GetAwaiter().GetResult();
//    uint256 hash = signedTx.GetHash();
//    var hashString = hash.ToString();
//}


/*
public static void SetMinerSecret(CoreNode coreNode, string walletName = "mywallet", string walletPassword = "password", string accountName = "account 0", string miningAddress = null)
{
    if (coreNode.MinerSecret == null)
    {
        HdAddress address;
        if (!string.IsNullOrEmpty(miningAddress))
        {
            address = coreNode.FullNode.WalletManager().GetAccounts(walletName).Single(a => a.Name == accountName).GetCombinedAddresses().Single(add => add.Address == miningAddress);
        }
        else
        {
            address = coreNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));
        }

        coreNode.MinerHDAddress = address;

        Wallet wallet = coreNode.FullNode.WalletManager().GetWalletByName(walletName);
        Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address).PrivateKey;
        coreNode.SetMinerSecret(new BitcoinSecret(extendedPrivateKey, coreNode.FullNode.Network));
    }
}
*/

//public static void SetMinerSecret(FullNode fullNode)
//{


//    string walletName = "blackstone";
//    string walletPassword = "fhdsjkfhjksdlhfjkdlshfkdshfk";
//    string accountName = "account 0";

//    string miningAddress = null;

//    if (_minerSecret == null)
//    {
//        HdAddress address;
//        if (!string.IsNullOrEmpty(miningAddress))
//        {
//            address = fullNode.WalletManager().GetAccounts(walletName).Single(a => a.Name == accountName).GetCombinedAddresses().Single(add => add.Address == miningAddress);
//        }
//        else
//        {
//            address = fullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));

//        }

//        _hdDddress = address;

//        Wallet wallet = fullNode.WalletManager().GetWalletByName(walletName);
//        Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address).PrivateKey;
//        _minerSecret = new BitcoinSecret(extendedPrivateKey, fullNode.Network);
//    }
//}
/*
public static (HdAddress AddressUsed, List<uint256> BlockHashes) MineBlocks(CoreNode node, int numberOfBlocks, bool syncNode = true, string walletName = "mywallet", string walletPassword = "password", string accountName = "account 0", string miningAddress = null)
{
    Guard.NotNull(node, nameof(node));

    if (numberOfBlocks == 0)
        throw new ArgumentOutOfRangeException(nameof(numberOfBlocks), "Number of blocks must be greater than zero.");

    SetMinerSecret(node, walletName, walletPassword, accountName, miningAddress);

    var script = new ReserveScript { ReserveFullNodeScript = node.MinerSecret.ScriptPubKey };
    var blockHashes = node.FullNode.Services.ServiceProvider.GetService<IPowMining>().GenerateBlocks(script, (ulong)numberOfBlocks, uint.MaxValue);

    if (syncNode)
        TestBase.WaitLoop(() => IsNodeSynced(node));

    return (node.MinerHDAddress, blockHashes);
}
*/
//public static WalletManager WalletManager(this FullNode fullNode)
//{
//    return fullNode.NodeService<IWalletManager>() as WalletManager;
//}

//public static IBroadcasterManager BroadcasterManager(this FullNode fullNode)
//{
//    return fullNode.NodeService<IBroadcasterManager>();
//}

//public static IBlockStore BlockStore(this FullNode fullNode)
//{
//    return fullNode.NodeService<IBlockStore>();
//}




