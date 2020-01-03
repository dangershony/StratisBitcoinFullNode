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

            var bytesr = new byte[4];
            this.reader.Read(bytesr, 0, 4);
            var sizet = BitConverter.ToInt32(bytesr); // this.reader.ReadInt32();
            var bytes = new byte[sizet];
            this.reader.Read(bytes, 0, sizet);
            obj.tableType = System.Text.Encoding.UTF8.GetString(bytes);

            bytesr = new byte[4];
            this.reader.Read(bytesr, 0, 4);
            var size = BitConverter.ToInt32(bytesr); // this.reader.ReadInt32();
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
            var len = BitConverter.GetBytes(bytes.Length);

            this.writer.Write(len);
            this.writer.Write(bytes);

            len = BitConverter.GetBytes(obj.key.Length);
            this.writer.Write(len);
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
            var bytesr = new byte[4];
            this.reader.Read(bytesr, 0, 4);
            var sizet = BitConverter.ToInt32(bytesr); // this.reader.ReadInt32();

            int size = BitConverter.ToInt32(bytesr); ; // this.reader.ReadInt32();
             obj.value = this.reader.ReadBytes(size);
        }

        public override void Serialize(ref CoinviewValue obj)
        {
            var len = BitConverter.GetBytes(obj.value.Length);
            this.writer.Write(len);
            this.writer.Write(obj.value);
            //this.writer.Write(obj.value);

        }
    }

    public class CoinviewInput
    {
        public byte[] value;
    }

    public class CoinviewOutput
    {
        public CoinviewValue value;
    }

    public class CoinviewContext
    {
        private Status status;
        private CoinviewOutput output;

        internal void Populate(ref Status status, ref CoinviewOutput output)
        {
            this.status = status;
            this.output = output;
        }

        internal void FinalizeRead(ref Status status, ref CoinviewOutput output)
        {
            status = this.status;
            output = this.output;
        }
    }

    public class CoinviewFunctions : IFunctions<CoinviewKey, CoinviewValue, CoinviewInput, CoinviewOutput, CoinviewContext>
    {
        public void RMWCompletionCallback(ref CoinviewKey key, ref CoinviewInput input, CoinviewContext ctx, Status status)
        {
        }

        public void ReadCompletionCallback(ref CoinviewKey key, ref CoinviewInput input, ref CoinviewOutput output, CoinviewContext ctx, Status status)
        {
            ctx.Populate(ref status, ref output);
        }


        public void UpsertCompletionCallback(ref CoinviewKey key, ref CoinviewValue value, CoinviewContext ctx)
        {
        }

        public void DeleteCompletionCallback(ref CoinviewKey key, CoinviewContext ctx)
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
            if (value.value.Length < input.value.Length)
                return false;

            value.value = input.value;
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
            if (src == null)
                return false;

            if (dst.value.Length != src.value.Length)
                return false;

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
