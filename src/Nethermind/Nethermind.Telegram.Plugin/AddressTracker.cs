// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Telegram.Plugin;

public class AddressTracker : IBlockTracer
{
    private readonly Action<Address, Address, UInt256> _callback;

    public AddressTracker(Action<Address, Address, UInt256> callback)
    {
        _callback = callback;
    }
    public bool IsTracingRewards => false;
    public void ReportReward(Address author, string rewardType, UInt256 rewardValue)
    {

    }

    public void StartNewBlockTrace(Block block)
    {

    }

    public ITxTracer StartNewTxTrace(Transaction? tx)
    {
        if (tx is not null && tx.SenderAddress is not null && tx.To is not null)
        {
            _callback(tx.SenderAddress, tx.To, tx.Value);
        }
        return NullTxTracer.Instance;
    }

    public void EndTxTrace()
    {

    }

    public void EndBlockTrace()
    {

    }
}
