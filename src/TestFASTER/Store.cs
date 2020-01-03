using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FASTER.core;

namespace TestFASTER
{
    public class Coinviewdb
    {
        private string dataFolder;

        public FasterKV<CoinviewKey, CoinviewValue, CoinviewInput, CoinviewOutput, CoinviewContext, CoinviewFunctions> db;
        public IDevice log;
        public IDevice objLog;

        public Coinviewdb(string folder)
        {
            this.dataFolder = folder;
        }

        public bool InitAndRecover()
        {
            var logSize = 1L << 20;
            this.log = Devices.CreateLogDevice(@$"{this.dataFolder}\data\coinview-hlog.log", preallocateFile: false);
            this.objLog = Devices.CreateLogDevice(@$"{this.dataFolder}\data\coinview-hlog-obj.log", preallocateFile: false);

            this.db = new FasterKV
                <CoinviewKey, CoinviewValue, CoinviewInput, CoinviewOutput, CoinviewContext, CoinviewFunctions>(
                    logSize, 
                    new CoinviewFunctions(), 
                    new LogSettings 
                    { 
                        LogDevice = this.log, 
                        ObjectLogDevice = this.objLog,
                        MutableFraction = 0.3,
                        PageSizeBits = 15,
                        MemorySizeBits = 20
                    },
                    new CheckpointSettings 
                    { 
                        CheckpointDir = $"{this.dataFolder}/data/checkpoints"
                    },
                    new SerializerSettings<CoinviewKey, CoinviewValue> 
                    { 
                        keySerializer = () => new CoinviewKeySerializer(), 
                        valueSerializer = () => new CoinviewValueSerializer() 
                    }
                );

            if (Directory.Exists($"{this.dataFolder}/data/checkpoints"))
            {
                Console.WriteLine("call rcover db");

                this.db.Recover();
                
                return false;
            }

            return true;
        }

        public Guid Checkpoint()
        {
            Guid token = default(Guid);

            Task.Run(() =>
            {
                this.db.TakeFullCheckpoint(out token);
                this.db.CompleteCheckpoint();

            }).Wait(); ;

            return token;
        }

        public void Dispose()
        {
            this.db.Dispose();
            this.log.Close();
            this.objLog.Close();
        }
    }
}
