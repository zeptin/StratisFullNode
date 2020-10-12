using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Airdrop
{
    // TODO: Need to put this feature into SBFN as well
    /// <summary>
    /// </summary>
    public class AirdropFeature : FullNodeFeature
    {
        private readonly AirdropSettings airdropSettings;
        private readonly AirdropManager airdropManager;

        public AirdropFeature(AirdropSettings airdropSettings, AirdropManager airdropManager)
        {
            this.airdropSettings = Guard.NotNull(airdropSettings, nameof(airdropSettings));
            this.airdropManager = Guard.NotNull(airdropManager, nameof(airdropManager));
        }

        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            AirdropSettings.PrintHelp(network);
        }
    }

    public static class AirdropFeatureExtension
    {
        public static IFullNodeBuilder UseAirdropFeature(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<AirdropFeature>("airdrop");

            fullNodeBuilder.ConfigureFeature(features =>
                features
                .AddFeature<AirdropFeature>()
                .FeatureServices(services => services
                    .AddSingleton<AirdropSettings>()
                    .AddSingleton<AirdropManager>()
                )
            );

            return fullNodeBuilder;
        }
    }
}
