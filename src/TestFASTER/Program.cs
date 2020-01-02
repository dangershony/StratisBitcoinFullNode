using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FASTER.core;
using NBitcoin;

namespace TestFASTER
{
    class Program
    {
        Dictionary<OutPoint, byte[]> utxodata = new Dictionary<OutPoint, byte[]>();

        static bool stop;

        static void Main(string[] args)
        {
            Task.Run(() => 
            {
                Console.WriteLine("Press anykey to stop");
                Console.ReadKey();
                stop = true;
            });

            //var dataFolder = System.IO.Path.GetDirectoryName(typeof(Program).Assembly.Location);

            Coinviewdb store = new Coinviewdb(@"C:\FasterTest\data");

            Console.WriteLine("call init db");
            var first = store.InitAndRecover();

            var lastblockKey = new CoinviewKey { tableType = "L", key = new byte[1] { 0 } }; // last block
            //var lastblockKey = new CoinviewKey { k = 1 };
            var lastBlockvalue = new CoinviewValue();

            if (first)
            {
                var ss = store.db.NewSession();

                lastBlockvalue = new CoinviewValue { value = Utils.ToBytes(0, false) }; // genesis
                //lastBlockvalue = new CoinviewValue { value = 0 };

                ss.Upsert(ref lastblockKey, ref lastBlockvalue, Empty.Default, 1);
              
                ss.CompletePending(true);
                ss.Dispose();

                store.Checkpoint();
               // store.db.Recover();
            }

            Task.Run(() =>
            {
                // insert loop
                var session = store.db.NewSession();

                while (stop == false)
                {
                    CoinviewInput input = new CoinviewInput(); 
                    CoinviewOutput output = new CoinviewOutput();
                    lastblockKey = new CoinviewKey { tableType = "L", key = new byte[1] { 0 } };
                    //lastblockKey = new CoinviewKey { k = 1 };
                    var blkStatus = session.Read(ref lastblockKey, ref input, ref output, Empty.Default, 1);
                    var blockHeight = Utils.ToUInt32(output.value.value, false);
                    //var blockHeight = output.value.value;
                    blockHeight += 1;


                    var start = blockHeight;
                    var toadd = start * 10000000;
                    for (long i = toadd; i < toadd + 1000; i++)
                    {
                        var data = Generate(i);

                        var upsertKey = new CoinviewKey { tableType = "C", key = data.outPoint.ToBytes(), outPoint = data.outPoint }; 
                        //var upsertKey = new CoinviewKey { k = i };
                        var upsertValue = new CoinviewValue { value = data.data };
                        //var upsertValue = new CoinviewValue { value = i };

                        var addStatus = session.Upsert(ref upsertKey, ref upsertValue, Empty.Default, 1);
                    }

                    if (blockHeight > 150)
                    {
                        toadd = (start - 5) * 10000000;

                        for (long i = toadd; i < toadd + 1000; i++)
                        {
                            if (i % 100 == 0)
                            {
                                var data = Generate(i);

                                var deteletKey = new CoinviewKey { tableType = "C", key = data.outPoint.ToBytes(), outPoint = data.outPoint };
                                //var deteletKey = new CoinviewKey { k = i };
                                var upsertValue = new CoinviewValue { value = data.data };
                                //var upsertValue = new CoinviewValue { value = i };

                                var deleteStatus = session.Delete(ref deteletKey, Empty.Default, 1);
                            }
                        }
                    }

                    Console.WriteLine(blockHeight + " processed");

                    lastBlockvalue = new CoinviewValue { value = Utils.ToBytes(blockHeight, false) };
                    //lastBlockvalue = new CoinviewValue { value = blockHeight };
                    lastblockKey = new CoinviewKey { tableType = "L", key = new byte[1] { 0 } };
                    //lastblockKey = new CoinviewKey { k = 1 };
                    session.Upsert(ref lastblockKey, ref lastBlockvalue, Empty.Default, 1);
                    session.CompletePending(true);

                    if (blockHeight % 1000 == 0)
                    {
                        session.Refresh();
                        Console.WriteLine(blockHeight + " refresh done" + blockHeight);
                    }

                    if (blockHeight % 5000 == 0)
                    {
                        store.Checkpoint();
                        Console.WriteLine(blockHeight + " checkpoint done" + blockHeight);
                    }
                }

                session.Dispose();

            }).Wait();
         
            store.Checkpoint();
            store.db.Dispose();

        }

        static (OutPoint outPoint, byte[] data) Generate(long i)
        {
            OutPoint p = new OutPoint(new uint256((ulong)(i * int.MaxValue)), (int)i);
            byte[] data = System.Text.Encoding.UTF8.GetBytes("00000000000000000000000000000000000000000000000000000000000000000000000000000000000" + i);

            return (p, data);
        }
    }
}
