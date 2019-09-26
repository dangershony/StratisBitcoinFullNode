using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Stratis.Bitcoin.Features.Airdrop
{
    public class UtxoContext : DbContext
    {
        private readonly string path;
        public DbSet<UTXOSnapshot> UnspentOutputs { get; set; }

        public DbSet<UTXODistribute> DistributeOutputs { get; set; }

        public UtxoContext(string path)
        {
            this.path = path;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($@"Data Source={this.path}\snapshot.db");
        }
    }

    public class UTXOSnapshot
    {
        [Key]
        public string Trxid { get; set; }

        public string Script { get; set; }
        public string Address { get; set; }
        public string ScriptType { get; set; }
        public long Value { get; set; }
        public int Height { get; set; }
    }

    public class UTXODistribute
    {
        [Key]
        public string Address { get; set; }

        public string Script { get; set; }
        public string ScriptType { get; set; }
        public long Value { get; set; }
        public int Height { get; set; }
        public string Status { get; set; }
    }

    public class DistributeStatus
    {
        public const string NoStarted = "";
        public const string Started = "started";
        public const string InProgress = "inprogress";
        public const string Complete = "complete";
        public const string Failed = "failed";
    }
}