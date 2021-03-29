﻿using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.ExternalApi.ApiClients;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.ExternalApi
{
    public class ExternalApiPoller : IDisposable
    {
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILogger logger;
        private readonly ExternalApiSettings externalApiSettings;
        private readonly EtherscanClient etherscanClient;
        private readonly CoinGeckoClient coinGeckoClient;

        private IAsyncLoop gasPriceLoop;
        private IAsyncLoop priceLoop;

        public ExternalApiPoller(NodeSettings nodeSettings,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            ExternalApiSettings externalApiSettings)
        {
            this.asyncProvider = asyncProvider;
            this.nodeLifetime = nodeLifetime;
            this.logger = nodeSettings.LoggerFactory.CreateLogger(this.GetType().FullName);
            this.externalApiSettings = externalApiSettings;
            this.etherscanClient = new EtherscanClient(this.externalApiSettings);
            this.coinGeckoClient = new CoinGeckoClient(this.externalApiSettings);
        }

        public void Initialize()
        {
            this.logger.LogInformation($"External API feature enabled, initializing periodic loops.");

            if (this.externalApiSettings.EthereumGasPriceTracking)
            {
                this.logger.LogInformation($"Ethereum gas price tracking enabled.");

                this.gasPriceLoop = this.asyncProvider.CreateAndRunAsyncLoop("PeriodicCheckGasPrice", async (cancellation) =>
                    {
                        this.logger.LogTrace("Beginning gas price check loop.");

                        try
                        {
                            await this.etherscanClient.GasOracle(true).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            this.logger.LogWarning("Exception raised when checking current gas price. {0}", e);
                        }

                        this.logger.LogTrace("Finishing gas price check loop.");
                    },
                    this.nodeLifetime.ApplicationStopping,
                    repeatEvery: TimeSpans.Minute,
                    startAfter: TimeSpans.TenSeconds);
            }

            if (this.externalApiSettings.PriceTracking)
            {
                this.logger.LogInformation($"Price tracking for STRAX and ETH enabled.");

                this.priceLoop = this.asyncProvider.CreateAndRunAsyncLoop("PeriodicCheckPrice", async (cancellation) =>
                    {
                        this.logger.LogTrace("Beginning price check loop.");

                        try
                        {
                            await this.coinGeckoClient.PriceDataRetrievalAsync().ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            this.logger.LogWarning("Exception raised when checking current prices. {0}", e);
                        }

                        this.logger.LogTrace("Finishing price check loop.");
                    },
                    this.nodeLifetime.ApplicationStopping,
                    repeatEvery: TimeSpans.Minute,
                    startAfter: TimeSpans.TenSeconds);
            }
        }

        public decimal GetStratisPrice()
        {
            return this.coinGeckoClient.GetStratisPrice();
        }

        public decimal GetEthereumPrice()
        {
            return this.coinGeckoClient.GetEthereumPrice();
        }

        public int GetGasPrice()
        {
            return this.etherscanClient.GetGasPrice();
        }

        public BigInteger EstimateConversionTransactionGas()
        {
            // The cost of submitting a multisig ERC20 transfer to the multisig contract.
            const decimal SubmissionGasCost = 230_000;

            // The cost of submitting a confirmation transaction to the multisig contract.
            const decimal ConfirmGasCost = 100_000;

            // The final confirmation that meets the contract threshold; this incurs slightly higher gas due to the transaction execution occurring as well.
            const decimal ExecuteGasCost = 160_000;

            var totalGas = new BigInteger(SubmissionGasCost + (7 * ConfirmGasCost) + ExecuteGasCost));

            int gasPrice = this.GetGasPrice();

            return totalGas * gasPrice;
        }

        public decimal EstimateConversionTransactionFee()
        {
            // The approximate USD fee that will be applied to conversion transactions, over and above the computed gas cost.
            const decimal ConversionTransactionFee = 100;

            var OneEther = new BigInteger(1_000_000_000_000_000_000);

            BigInteger overallGas = this.EstimateConversionTransactionGas();

            // WIP

            decimal ethereumUsdPrice = this.GetEthereumPrice();

            decimal stratisPrice = this.GetStratisPrice();
        }

        public void Dispose()
        {
            this.gasPriceLoop?.Dispose();
            this.priceLoop?.Dispose();
        }
    }
}
