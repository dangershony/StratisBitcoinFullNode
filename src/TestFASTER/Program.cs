using System;
using System.Collections.Generic;
using System.Linq;
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
                Console.WriteLine("Press any key to stop");
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

                lastBlockvalue = new CoinviewValue { value = Utils.ToBytes(0, true) }; // genesis
                //lastBlockvalue = new CoinviewValue { value = 0 };
                CoinviewContext context1 = new CoinviewContext();
                ss.Upsert(ref lastblockKey, ref lastBlockvalue, context1, 1);
              
                ss.CompletePending(true);
                ss.Dispose();

                store.Checkpoint();
            }

            Task.Run(() => 
            {
                var session = store.db.NewSession();

                // test all data up to now.
                CoinviewInput input = new CoinviewInput();
                CoinviewOutput output = new CoinviewOutput();
                lastblockKey = new CoinviewKey { tableType = "L", key = new byte[1] { 0 } };
               // lastblockKey = new CoinviewKey { k = 1 };
                CoinviewContext context1 = new CoinviewContext();
                var blkStatus = session.Read(ref lastblockKey, ref input, ref output, context1, 1);
                var blockHeight = Utils.ToUInt32(output.value.value, true);

                var from = 1;

                while (from <= blockHeight)
                {
                    var start = from;
                    var toadd = start * 10000000;
                    for (long i = toadd; i < toadd + 1000; i++)
                    {
                        var data = Generate(i);

                        CoinviewInput input1 = new CoinviewInput();
                        CoinviewOutput output1 = new CoinviewOutput();
                        CoinviewContext context = new CoinviewContext();
                        var readKey = new CoinviewKey { tableType = "C", key = data.outPoint.ToBytes(), outPoint = data.outPoint };
                       // var readKey = new CoinviewKey { k = i };
                        var addStatus = session.Read(ref readKey, ref input1, ref output1, context, 1);
                        if (addStatus == Status.PENDING)
                        {
                            session.CompletePending(true);
                            context.FinalizeRead(ref addStatus, ref output1);
                        }

                        if (addStatus != Status.OK)
                            throw new Exception();
                       // if (output1.value.value.SequenceEqual(data.data) == false)
                         //   throw new Exception();
                    }

                    if (from > 150)
                    {
                        toadd = (start - 5) * 10000000;

                        for (long i = toadd; i < toadd + 1000; i++)
                        {
                            if (i % 100 == 0)
                            {
                                var data = Generate(i);

                                CoinviewInput input1 = new CoinviewInput();
                                CoinviewOutput output1 = new CoinviewOutput();
                                CoinviewContext context = new CoinviewContext();
                                var readKey = new CoinviewKey { tableType = "C", key = data.outPoint.ToBytes(), outPoint = data.outPoint };
                                //var readKey = new CoinviewKey { k = i };

                                var deleteStatus = session.Read(ref readKey, ref input1, ref output1, context, 1);

                                if (deleteStatus == Status.PENDING)
                                {
                                    session.CompletePending(true);
                                    context.FinalizeRead(ref deleteStatus, ref output1);
                                }

                                if (deleteStatus != Status.NOTFOUND)
                                    throw new Exception();
                            }
                        }
                    }

                    from++;
                }

                session.Dispose();

            }).Wait();

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
                    CoinviewContext context1 = new CoinviewContext();
                    var blkStatus = session.Read(ref lastblockKey, ref input, ref output, context1, 1);
                    var blockHeight = Utils.ToUInt32(output.value.value, true);
                    //var blockHeight = output.value.value;
                    blockHeight += 1;


                    var start = blockHeight;
                    var toadd = start * 10000000;
                    for (long i = toadd; i < toadd + 1000; i++)
                    {
                        var data = Generate(i);

                        var upsertKey = new CoinviewKey { tableType = "C", key = data.outPoint.ToBytes(), outPoint = data.outPoint };
                       // var upsertKey = new CoinviewKey { k = i };
                        var upsertValue = new CoinviewValue { value = data.data };
                        //var upsertValue = new CoinviewValue { value = i };

                        CoinviewContext context2 = new CoinviewContext();
                        var addStatus = session.Upsert(ref upsertKey, ref upsertValue, context2, 1);
                        if(addStatus != Status.OK)
                            throw new Exception();
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
                                //var upsertValue = new CoinviewValue { value = data.data };
                                //var upsertValue = new CoinviewValue { value = i };

                                CoinviewContext context2 = new CoinviewContext();
                                var deleteStatus = session.Delete(ref deteletKey, context2, 1);
                                if (deleteStatus != Status.OK)
                                    throw new Exception();
                            }
                        }
                    }

                    Console.WriteLine(blockHeight + " processed");

                    lastBlockvalue = new CoinviewValue { value = Utils.ToBytes(blockHeight, true) };
                    //lastBlockvalue = new CoinviewValue { value = blockHeight };
                    lastblockKey = new CoinviewKey { tableType = "L", key = new byte[1] { 0 } };
                   // lastblockKey = new CoinviewKey { k = 1 };
                    CoinviewContext context = new CoinviewContext();
                    session.Upsert(ref lastblockKey, ref lastBlockvalue, context, 1);
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

                    stop = true;
                }

                session.Dispose();

            }).Wait();
         
            store.Checkpoint();
            store.Dispose();

        }

        static (OutPoint outPoint, byte[] data) Generate(long i)
        {
            OutPoint p = new OutPoint(new uint256((ulong)(i * int.MaxValue)), (int)i);
            byte[] data = System.Text.Encoding.UTF8.GetBytes("00000000000000000000000000000000000000000000000000000000000000000000000000000000000" + i);

            return (p, data);
        }
    }
}
