// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Api.Steps;

namespace Nethermind.Init.Steps
{
    [RunnerStepDependencies(typeof(InitializeBlockchain))]
    public class StartBlockProcessor : IStep
    {
        private readonly IApiWithBlockchain _api;

        public StartBlockProcessor(INethermindApi api)
        {
            _api = api;
        }

        public Task Execute(CancellationToken _)
        {
            if (_api.MainProcessingContext is null)
            {
                throw new StepDependencyException(nameof(_api.MainProcessingContext));
            }

            _api.MainProcessingContext.BlockchainProcessor.Start();
            return Task.CompletedTask;
        }
    }
}
