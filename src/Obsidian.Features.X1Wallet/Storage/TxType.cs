namespace Obsidian.Features.X1Wallet.Storage
{
    public enum TxType
    {
        /// <summary>
        /// The value has not been set.
        /// </summary>
        NotSet = 0,

        /// <summary>
        /// Coinbase transaction.
        /// </summary>
        Coinbase = 10,

        /// <summary>
        /// Legacy Coinstake transaction with 3 outputs.
        /// </summary>
        CoinstakeLegacy = 20,

        /// <summary>
        /// Coinstake transaction with 3 outputs.
        /// </summary>
        Coinstake = 21,

        /// <summary>
        /// The transaction spent wallet outputs, no outputs were received. 
        /// </summary>
        Spend = 30,

        /// <summary>
        /// The transaction added outputs to the wallet, but its type was not Coinbase, Legacy Coinstake or coinstake.
        /// </summary>
        Receive = 31,

        /// <summary>
        /// The transaction added outputs to the wallet, but they were not Coinbase, Legacy Coinstake or coinstake.
        /// In addition to that, the wallet received new unspent outputs. This normally means sending funds to oneself.
        /// </summary>
        WithinWallet = 32,

        SpendWithoutChange = 33,

        /// <summary>
        /// Legacy ColdCoinstake.
        /// </summary>
        ColdCoinstakeLegacy = 40,

        /// <summary>
        /// ColdCoinstake.
        /// </summary>
        ColdCoinstake = 41
    }
}