using NBitcoin;

namespace Stratis.Bitcoin.Networks
{
    public static class Networks
    {
        public static NetworksSelector Bitcoin
        {
            get
            {
                return new NetworksSelector(() => new BitcoinMain(), () => new BitcoinTest(), () => new BitcoinRegTest());
            }
        }

        public static NetworksSelector Stratis
        {
            get
            {
                return new NetworksSelector(() => new StratisMain(), () => new StratisTest(), () => new StratisRegTest());
            }
        }

        public static NetworksSelector Solaris
        {
            get
            {
                return new NetworksSelector(() => new SolarisMain(), () => new SolarisTest(), () => new SolarisRegTest());
            }
        }
    }
}
