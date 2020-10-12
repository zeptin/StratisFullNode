using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Airdrop
{
    /// <summary>
    /// Configuration related to the Airdrop feature.
    /// </summary>
    public class AirdropSettings
    {
        private readonly ILogger logger;

        public bool DistributionEnabled { get; set; }

        public string WalletName { get; set; }

        public string AccountName { get; set; }

        public string WalletPassword { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public AirdropSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(this.GetType().FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.DistributionEnabled = config.GetOrDefault("airdropdistribute", false, this.logger);
            this.WalletName = config.GetOrDefault("airdropwallet", "", this.logger);
            this.AccountName = config.GetOrDefault("airdropaccount", "", this.logger);
            this.WalletPassword = config.GetOrDefault("airdroppassword", "", this.logger);
        }

        /// <summary>Prints the help information on how to configure the Airdrop Feature to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-airdropdistribute=<bool>    Run the distribution as a background task.");
            builder.AppendLine($"-airdropwallet=<name>        The name of the wallet to use as a source of funds for distribution.");
            builder.AppendLine($"-airdropaccount=<name>       The name of the account within the wallet to use for distribution.");
            builder.AppendLine($"-airdroppassword=<password>  The wallet password to use for distribution.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }
    }
}