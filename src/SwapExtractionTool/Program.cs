﻿using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Networks;

namespace SwapExtractionTool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int stratisNetworkApiPort;
            int startBlock = 0;
            Network straxNetwork;

            if (args.Contains("-testnet"))
            {
                startBlock = 1_450_000;

                stratisNetworkApiPort = 38221;
                straxNetwork = new StraxTest();
            }
            else
            {
                startBlock = 1_949_800;

                stratisNetworkApiPort = 37221;
                straxNetwork = new StraxMain();
            }

            var service = new SwapExtractionService(stratisNetworkApiPort, straxNetwork);

            var arg = args.FirstOrDefault(a => a.StartsWith("-startfrom"));
            if (arg != null)
                int.TryParse(arg.Split('=')[1], out startBlock);

            if (args.Contains("-swap"))
                await service.RunAsync(ExtractionType.Swap, startBlock);

            if (args.Contains("-vote"))
                await service.RunAsync(ExtractionType.Vote, startBlock);
        }
    }
}
