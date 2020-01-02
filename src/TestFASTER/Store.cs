﻿using System;
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

        public FasterKV<CoinviewKey, CoinviewValue, CoinviewInput, CoinviewOutput, Empty, CoinviewFunctions> db;

        public Coinviewdb(string folder)
        {
            this.dataFolder = folder;
        }

        public bool InitAndRecover()
        {
            var logSize = 1L << 20;
            var log = Devices.CreateLogDevice(@$"{this.dataFolder}\data\coinview-hlog.log", preallocateFile: false);
            var logObg = Devices.CreateLogDevice(@$"{this.dataFolder}\data\coinview-hlog-obj.log", preallocateFile: false);

            this.db = new FasterKV
                <CoinviewKey, CoinviewValue, CoinviewInput, CoinviewOutput, Empty, CoinviewFunctions>(
                    logSize, 
                    new CoinviewFunctions(), 
                    new LogSettings 
                    { 
                        LogDevice = log, 
                        ObjectLogDevice = logObg,
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
    }
}
