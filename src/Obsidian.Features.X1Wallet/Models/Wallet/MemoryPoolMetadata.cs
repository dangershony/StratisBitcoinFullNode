using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;

namespace Obsidian.Features.X1Wallet.Models.Wallet
{
    public class MemoryPoolMetadata 
    {
        public HashSet<MemoryPoolEntry> Entries;
    }

    public class MemoryPoolEntry : IEquatable<MemoryPoolEntry>
    {
        public TransactionMetadata Transaction;

        [JsonConverter(typeof(StringEnumConverter))]
        public BroadcastState BroadcastState;

        public string ConsensusError;
        public string MemoryPoolError;

        #region overrides of Equals, GetHashCode, ==, !=

        public override bool Equals(object obj)
        {
            return Equals(obj as MemoryPoolEntry);
        }

        public bool Equals(MemoryPoolEntry other)
        {
            return other != null &&
                   this.Transaction == other.Transaction;
        }

        public override int GetHashCode()
        {
            return -1052816746 + EqualityComparer<TransactionMetadata>.Default.GetHashCode(this.Transaction);
        }

        public static bool operator ==(MemoryPoolEntry left, MemoryPoolEntry right)
        {
            return EqualityComparer<MemoryPoolEntry>.Default.Equals(left, right);
        }

        public static bool operator !=(MemoryPoolEntry left, MemoryPoolEntry right)
        {
            return !(left == right);
        }

        #endregion
    }

    public enum BroadcastState
    {
        NotSet = 0,
        NotRequested = 1,
        ToBroadcast = 10,
        Broadcasted = 20,
        Propagated = 25,
        CantBroadcast = 50,
        
    }

    public static class MemoryPoolExtensions
    {
        public static BroadcastState ToBroadcastState(this State state)
        {
            switch (state)
            {
                case State.Broadcasted:
                    return BroadcastState.Broadcasted;
                case State.CantBroadcast:
                    return BroadcastState.CantBroadcast;
                case State.ToBroadcast:
                    return BroadcastState.ToBroadcast;
                case State.Propagated:
                    return BroadcastState.Propagated;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public static string GetMemoryPoolError(this MempoolError mempoolError)
        {
            if (mempoolError == null)
                return null;
            return $"M-{mempoolError.RejectCode}-{mempoolError.Code}";
        }

        public static string GetMemoryPoolConsensusError(this MempoolError mempoolError)
        {
            if (mempoolError == null || mempoolError.ConsensusError == null)
                return null;
            return $"M-{mempoolError.ConsensusError.Code}-{mempoolError.ConsensusError.Message}";
        }
    }
}
