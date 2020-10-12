using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Features.Airdrop.Controllers
{
    /// <summary>
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]/[action]")]
    public class AirdropController : FeatureController
    {
        private readonly ILogger logger;
        private readonly IUtxoIndexer utxoIndexer;
        private readonly AirdropManager airdropManager;

        public AirdropController(IConnectionManager connectionManager, IConsensusManager consensusManager, IUtxoIndexer utxoIndexer, AirdropManager airdropManager, ILoggerFactory loggerFactory)
            : base(connectionManager: connectionManager, consensusManager: consensusManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.utxoIndexer = utxoIndexer;
        }

        /// <summary>Records the UTXO set at a given block height to a database. This may take some time for large chains.</summary>
        /// <param name="atBlockHeight">Only process blocks up to this height for the purposes of constructing the UTXO set.</param>
        /// <returns>Success (true) or failure.</returns>
        /// <response code="200">Returns success.</response>
        /// <response code="400">Unexpected exception occurred</response>
        [Route("takesnapshot")]
        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public IActionResult TakeSnapshot(int atBlockHeight)
        {
            try
            {
                ReconstructedCoinviewContext coinView = this.utxoIndexer.GetCoinviewAtHeight(atBlockHeight);
                
                foreach (OutPoint outPoint in coinView.UnspentOutputs)
                {
                    TxOut txOut = coinView.Transactions[outPoint.Hash].Outputs[outPoint.N];
                    
                    // Write the UTXO out to a separate database for later distribution.
                    this.airdropManager.Add(outPoint.Hash, (int)outPoint.N, txOut);
                }

                // We don't return the actual UTXO set here as it may be quite large, and there is already a separate API method for that if required.
                return this.Json(true);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("stopdistribution")]
        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public IActionResult StopDistribution()
        {
            try
            {
                this.airdropManager.Halt();

                return this.Json(true);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
