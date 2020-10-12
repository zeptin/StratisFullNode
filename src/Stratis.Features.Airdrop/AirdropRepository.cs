using System;
using System.Collections.Generic;
using SQLite;

namespace Stratis.Features.Airdrop
{
    public class AirdropRepository
    {
        private readonly SQLiteConnection db;

        public AirdropRepository(string databasePath)
        {
            this.db = new SQLiteConnection(databasePath);
            this.db.CreateTable<Distribution>();
        }

        public void Insert(Distribution distribution)
        {
            this.db.Insert(distribution);
        }

        public void Insert(DistributionTx distributionTx)
        {
            this.db.Insert(distributionTx);
        }

        public void Update(Distribution distribution)
        {
            this.db.Update(distribution);
        }

        public IEnumerable<Distribution> SelectNotDistributed(int count = Int32.MaxValue)
        {
            return this.db.Query<Distribution>("SELECT * FROM Distribution WHERE Distributed = ? LIMIT ?", false, count);
        }
    }

    public class Distribution
    {
        [PrimaryKey]
        public string SourceTxId { get; set; }

        [PrimaryKey]
        public int SourceTxIndex { get; set; }

        /// <summary>
        /// This is not a mandatory field but is included for streamlining potential support queries.
        /// Only non-empty if the source scriptPubKey has a valid address representation
        /// (the exception being P2PK scripts, which have no defined address format but are traditionally
        /// represented with the P2PKH address that corresponds to their public key).
        /// </summary>
        public string SourceAddress { get; set; }

        /// <summary>
        /// The network-invariant destination that the funds should be sent to.
        /// </summary>
        public string ScriptPubKey { get; set; }

        /// <summary>
        /// The amount to be distributed, in satoshi.
        /// </summary>
        public long Amount { get; set; }

        public string DistributionTxId { get; set; }

        /// <summary>
        /// This is not a mandatory field but is included for streamlining potential support queries.
        /// </summary>
        public string DistributionAddress { get; set; }

        public bool Distributed { get; set; }
    }

    /// <summary>
    /// Maintains a record of what was contained in each distribution transaction, for support purposes.
    /// </summary>
    public class DistributionTx
    {
        [PrimaryKey]
        public string TxId { get; set; }

        public string TransactionHex { get; set; }

        // TODO: Could perhaps maintain a broadcasted/confirmed state here
    }
}
