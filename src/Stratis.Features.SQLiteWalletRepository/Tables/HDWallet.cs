﻿using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using SQLite;
using Stratis.Features.SQLiteWalletRepository.Extensions;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class HDWallet
    {
        public string Name { get; set; }
        [PrimaryKey]
        public int WalletId { get; set; }
        public int LastBlockSyncedHeight { get; set; }
        public string LastBlockSyncedHash { get; set; }
        public bool IsExtPubKeyWallet { get; set; }
        public string EncryptedSeed { get; set; }
        public string ChainCode { get; set; }
        public string BlockLocator { get; set; }
        public int CreationTime { get; set; }

        internal static IEnumerable<string> CreateScript()
        {
            yield return $@"
            CREATE TABLE HDWallet (
                Name                TEXT NOT NULL,
                WalletId            INTEGER PRIMARY KEY AUTOINCREMENT,
                LastBlockSyncedHeight INTEGER NOT NULL,
                LastBlockSyncedHash TEXT NOT NULL,
                IsExtPubKeyWallet   INTEGER NOT NULL,
                EncryptedSeed       TEXT NOT NULL UNIQUE,
                ChainCode           TEXT NOT NULL,
                BlockLocator        TEXT NOT NULL,
                CreationTime        INTEGER NOT NULL
            );";
        }

        internal static void CreateTable(DBConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }

        internal void CreateWallet(SQLiteConnection conn)
        {
            conn.Execute($@"
            REPLACE INTO HDWallet (
                    Name
            ,       LastBlockSyncedHeight
            ,       LastBlockSyncedHash
            ,       IsExtPubKeyWallet
            ,       EncryptedSeed
            ,       ChainCode
            ,       BlockLocator
            ,       CreationTime
            )
            VALUES ('{this.Name}'
            ,       {this.LastBlockSyncedHeight}
            ,       '{this.LastBlockSyncedHash}'
            ,       {this.IsExtPubKeyWallet}
            ,       '{this.EncryptedSeed}'
            ,       '{this.ChainCode}'
            ,       '{this.BlockLocator}'
            ,       {this.CreationTime})");

            this.WalletId = conn.ExecuteScalar<int>($@"
            SELECT  WalletId
            FROM    HDWallet
            WHERE   Name = '{this.Name}'");
        }

        internal static HDWallet GetByName(SQLiteConnection conn, string walletName)
        {
            return conn.FindWithQuery<HDWallet>($@"
                SELECT *
                FROM   HDWallet
                WHERE  Name = ?", walletName);
        }

        internal static IEnumerable<HDWallet> GetAll(SQLiteConnection conn)
        {
            return conn.Query<HDWallet>($@"
                SELECT *
                FROM   HDWallet");
        }

        internal static HDWallet GetWalletByEncryptedSeed(SQLiteConnection conn, string encryptedSeed)
        {
            return conn.FindWithQuery<HDWallet>($@"
                SELECT *
                FROM   HDWallet
                WHERE  EncryptedSeed = ?", encryptedSeed);
        }

        internal static void AdvanceTip(SQLiteConnection conn, HDWallet wallet, ChainedHeader newTip, ChainedHeader prevTip)
        {
            uint256 lastBlockSyncedHash = newTip?.HashBlock ?? uint256.Zero;
            int lastBlockSyncedHeight = newTip?.Height ?? -1;
            string blockLocator = "";
            if (newTip != null)
                blockLocator = string.Join(",", newTip?.GetLocator().Blocks);

            conn.Execute($@"
                    UPDATE HDWallet
                    SET    LastBlockSyncedHash = '{lastBlockSyncedHash}',
                           LastBlockSyncedHeight = {lastBlockSyncedHeight},
                           BlockLocator = '{blockLocator}'
                    WHERE  LastBlockSyncedHash = '{(prevTip?.HashBlock ?? uint256.Zero)}' {
                    // Respect the wallet name if provided.
                    ((wallet?.Name != null) ? $@"
                    AND    Name = '{wallet?.Name}'" : "")}");
        }

        internal void SetLastBlockSynced(ChainedHeader lastBlockSynced)
        {
            uint256 lastBlockSyncedHash = lastBlockSynced?.HashBlock ?? uint256.Zero;
            int lastBlockSyncedHeight = lastBlockSynced?.Height ?? -1;
            string blockLocator = "";
            if (lastBlockSynced != null)
                blockLocator = string.Join(",", lastBlockSynced?.GetLocator().Blocks);

            this.LastBlockSyncedHash = lastBlockSyncedHash.ToString();
            this.LastBlockSyncedHeight = lastBlockSyncedHeight;
            this.BlockLocator = blockLocator;
        }

        internal bool WalletContainsBlock(ChainedHeader lastBlockSynced)
        {
            if (lastBlockSynced == null)
                return true;

            if (lastBlockSynced.Height > this.LastBlockSyncedHeight)
                return false;

            if (lastBlockSynced.Height == this.LastBlockSyncedHeight)
                return lastBlockSynced.HashBlock == uint256.Parse(this.LastBlockSyncedHash);

            var blockLocator = new BlockLocator()
            {
                Blocks = this.BlockLocator.Split(',').Select(strHash => uint256.Parse(strHash)).ToList()
            };

            List<int> locatorHeights = ChainedHeaderExt.GetLocatorHeights(this.LastBlockSyncedHeight);

            for (int i = 0; i < locatorHeights.Count; i++)
            {
                if (lastBlockSynced.Height >= locatorHeights[i])
                {
                    lastBlockSynced = lastBlockSynced.GetAncestor(locatorHeights[i]);

                    if (lastBlockSynced.HashBlock != blockLocator.Blocks[i])
                        return false;

                    break;
                }
            }

            return true;
        }
    }
}