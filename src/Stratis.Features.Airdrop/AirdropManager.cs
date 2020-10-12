using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Features.Wallet.Services;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Airdrop
{
    public class AirdropManager : IDisposable
    {
        /// <summary>
        /// Typically this database would be initially populated on one chain, and then manually moved to a node
        /// on another chain for actual distribution to be performed.
        /// </summary>
        public const string AirdropDatabaseName = "airdrop.db";

        /// <summary>
        /// The number of transaction outputs to be added to each distribution transaction. We roughly estimate
        /// that it is the size of the outputs that will comprise the majority of the transaction size in
        /// typical usage.
        /// </summary>
        public const int AirdropBatchSize = 100;

        private readonly NodeSettings nodeSettings;
        private readonly AirdropSettings settings;
        private readonly AirdropRepository repository;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly IScriptAddressReader scriptAddressReader;
        private readonly IWalletService walletService;

        private IAsyncLoop asyncLoop;
        private bool haltFlag;

        public AirdropManager(NodeSettings nodeSettings, AirdropSettings settings, INodeLifetime nodeLifetime, IScriptAddressReader scriptAddressReader, IWalletService walletService)
        {
            this.nodeSettings = nodeSettings;
            this.settings = settings;
            this.nodeLifetime = nodeLifetime;
            this.scriptAddressReader = scriptAddressReader;
            this.walletService = walletService;
            
            var databasePath = Path.Combine(this.nodeSettings.DataDir, AirdropDatabaseName);
            this.repository = new AirdropRepository(databasePath);

            if (this.settings.DistributionEnabled)
                this.Start();
        }

        public void Add(uint256 sourceTx, int sourceIndex, TxOut txOut)
        {
            var distribution = new Distribution()
            {
                SourceTxId = sourceTx.ToString(),
                SourceTxIndex = sourceIndex,
                SourceAddress = this.scriptAddressReader.GetAddressFromScriptPubKey(this.nodeSettings.Network, txOut.ScriptPubKey),
                ScriptPubKey = txOut.ScriptPubKey.ToHex(),
                Amount = txOut.Value
            };

            this.repository.Insert(distribution);
        }

        public void Start()
        {
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(AirdropManager), async token => {
                    await this.DoDistributionAsync().ConfigureAwait(false);
                },
                this.nodeLifetime.ApplicationStopping,
                TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// This should not be called unless absolutely necessary.
        /// </summary>
        public void Halt()
        {
            this.haltFlag = true;
        }

        public async Task DoDistributionAsync()
        {
            IEnumerable<Distribution> unprocessed = this.repository.SelectNotDistributed(AirdropBatchSize);

            // Build transaction
            var recipients = new List<RecipientModel>();

            foreach (Distribution distribution in unprocessed)
            {
                Script destination = Script.FromHex(distribution.ScriptPubKey);

                // Set the distribution address for information purposes.
                distribution.DistributionAddress = this.scriptAddressReader.GetAddressFromScriptPubKey(this.nodeSettings.Network, destination);

                // We may be airdropping to a script type that lacks a defined address format, so construct the recipient with only the script.
                recipients.Add(new RecipientModel() { DestinationScript = distribution.ScriptPubKey, Amount = Money.Satoshis(distribution.Amount).ToUnit(MoneyUnit.BTC).ToString() });

                // Update repository entry as processed. This is for safety in case there is a crash and the distribution gets run again.
                // Some manual verification may be necessary for transactions that are marked as distributed but lack a destination txid.
                distribution.Distributed = true;

                this.repository.Update(distribution);
            }

            var request = new BuildTransactionRequest
            {
                WalletName = this.settings.WalletName,
                AccountName = this.settings.AccountName,
                FeeType = "medium",
                Password = this.settings.WalletPassword,
                Recipients = recipients
            };

            WalletBuildTransactionModel transactionModel = await this.walletService.BuildTransaction(request, default(CancellationToken));

            this.repository.Insert(new DistributionTx() { TxId = transactionModel.TransactionId.ToString(), TransactionHex = transactionModel.Hex });

            await this.walletService.SendTransaction(new SendTransactionRequest() {Hex = transactionModel.Hex}, default(CancellationToken));

            // Update repository with sent transaction details.
            foreach (Distribution distribution in unprocessed)
            {
                distribution.DistributionTxId = transactionModel.TransactionId.ToString();

                this.repository.Update(distribution);
            }

            // Cause the loop to fault if a halt has been requested.
            if (this.haltFlag)
                throw new Exception("Halt requested");
        }

        public void Dispose()
        {
            this.Stop();
        }

        private void Stop()
        {
            if (this.asyncLoop != null)
            {
                this.asyncLoop.Dispose();
                this.asyncLoop = null;
            }
        }
    }
}
