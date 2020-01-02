using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FASTER.core;
using NBitcoin;
using NBitcoin.Crypto;

namespace TestFASTER
{
    public class CoinviewKey : IFasterEqualityComparer<CoinviewKey>
    {
        //public long k;
        public byte[] key;
        public string tableType;
        public OutPoint outPoint;

        public virtual long GetHashCode64(ref CoinviewKey key)
        {
            //return key.k;

            if (key.tableType == "C")
            {
                return key.outPoint.GetHashCode();
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(key.tableType);
            byte[] b = bytes.ToArray().Concat(key.key).ToArray();

            var hash256 = Hashes.Hash256(b);

            //var hash = key.tableType ^ hash256.GetHashCode();
            var hash = hash256.GetHashCode();
            return Utility.GetHashCode(hash);
        }

        public virtual bool Equals(ref CoinviewKey k1, ref CoinviewKey k2)
        {
            // return k1.k == k2.k;

            if (this.tableType == "C")
            {
                return k1.outPoint == k2.outPoint;
            }

            return k1.key.SequenceEqual(k2.key) && k1.tableType == k2.tableType;
        }
    }

    public class CoinviewKeySerializer : BinaryObjectSerializer<CoinviewKey>
    {
        public override void Deserialize(ref CoinviewKey obj)
        {
            // obj.k = this.reader.ReadInt64();

            var sizet = this.reader.ReadInt32();
            var bytes = new byte[sizet];
            this.reader.Read(bytes, 0, sizet);
            obj.tableType = System.Text.Encoding.UTF8.GetString(bytes);

            var size = this.reader.ReadInt32();
            obj.key = new byte[size];
            this.reader.Read(obj.key, 0, size);


            if (obj.tableType == "C")
            {
                obj.outPoint = new OutPoint();
                obj.outPoint.FromBytes(obj.key);
            }
        }

        public override void Serialize(ref CoinviewKey obj)
        {
            //this.writer.Write(obj.k);
            var bytes = System.Text.Encoding.UTF8.GetBytes(obj.tableType);
            this.writer.Write(bytes.Length);
            this.writer.Write(bytes);

            this.writer.Write(obj.key.Length);
            this.writer.Write(obj.key);
        }
    }

    public class CoinviewValue
    {
        public byte[] value;
        //public long value;
        public CoinviewValue()
        {
        }
    }

    public class CoinviewValueSerializer : BinaryObjectSerializer<CoinviewValue>
    {
        public override void Deserialize(ref CoinviewValue obj)
        {
            //obj.value = this.reader.ReadInt64();
             int size = this.reader.ReadInt32();
             obj.value = this.reader.ReadBytes(size);
        }

        public override void Serialize(ref CoinviewValue obj)
        {
            this.writer.Write(obj.value.Length);
            this.writer.Write(obj.value);
            //this.writer.Write(obj.value);

        }
    }

    public class CoinviewInput
    {
        public int value;
    }

    public class CoinviewOutput
    {
        public CoinviewValue value;
    }

    public class CoinviewFunctions : IFunctions<CoinviewKey, CoinviewValue, CoinviewInput, CoinviewOutput, Empty>
    {
        public void RMWCompletionCallback(ref CoinviewKey key, ref CoinviewInput input, Empty ctx, Status status)
        {
        }

        public void ReadCompletionCallback(ref CoinviewKey key, ref CoinviewInput input, ref CoinviewOutput output, Empty ctx, Status status)
        {
        }


        public void UpsertCompletionCallback(ref CoinviewKey key, ref CoinviewValue value, Empty ctx)
        {
        }

        public void DeleteCompletionCallback(ref CoinviewKey key, Empty ctx)
        {
        }

        public void CopyUpdater(ref CoinviewKey key, ref CoinviewInput input, ref CoinviewValue oldValue, ref CoinviewValue newValue)
        {
        }

        public void InitialUpdater(ref CoinviewKey key, ref CoinviewInput input, ref CoinviewValue value)
        {
        }

        public bool InPlaceUpdater(ref CoinviewKey key, ref CoinviewInput input, ref CoinviewValue value)
        {
            return true;
        }

        public void SingleReader(ref CoinviewKey key, ref CoinviewInput input, ref CoinviewValue value, ref CoinviewOutput dst)
        {
            dst.value = value;
        }

        public void ConcurrentReader(ref CoinviewKey key, ref CoinviewInput input, ref CoinviewValue value, ref CoinviewOutput dst)
        {
            dst.value = value;
        }

        public bool ConcurrentWriter(ref CoinviewKey key, ref CoinviewValue src, ref CoinviewValue dst)
        {
            dst = src;
            return true;
        }

        public void CheckpointCompletionCallback(string sessionId, CommitPoint commitPoint)
        {
        }

        public void SingleWriter(ref CoinviewKey key, ref CoinviewValue src, ref CoinviewValue dst)
        {
            dst = src;
        }
    }
}
