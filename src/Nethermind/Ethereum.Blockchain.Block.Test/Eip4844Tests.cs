// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ethereum.Test.Base;
using NUnit.Framework;

namespace Ethereum.Blockchain.Block.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class Eip4844Tests : BlockchainTestBase
{
    [TestCaseSource(nameof(LoadTests))]
    public async Task Test(BlockchainTest test)
    {
        await RunTest(test);
    }

    public static IEnumerable<BlockchainTest> LoadTests()
    {
        var loaderBlobHash =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "blobhash_opcode").LoadTests();
        var loaderBlobHashOpcode =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "blobhash_opcode_contexts").LoadTests();
        var loaderBlobTx =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "blob_txs").LoadTests();
        var loaderBlobTxFull =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "blob_txs_full").LoadTests();
        var loaderExcessGas =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "excess_data_gas").LoadTests();
        var loaderExcessGasTransition =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "excess_data_gas_fork_transition").LoadTests();
        var loaderPointEval =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "point_evaluation_precompile").LoadTests();
        var loaderPointEvalGas =
            (IEnumerable<BlockchainTest>)new TestsSourceLoader(new LoadBlockchainTestsStrategy(), "point_evaluation_precompile_gas").LoadTests();

        return loaderBlobHash
            .Concat(loaderBlobHashOpcode)
            .Concat(loaderBlobTx)
            .Concat(loaderBlobTxFull)
            .Concat(loaderExcessGas)
            .Concat(loaderExcessGasTransition)
            .Concat(loaderPointEval)
            .Concat(loaderPointEvalGas);
    }
}
