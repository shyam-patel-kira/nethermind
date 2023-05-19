// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.State
{
    public class InsufficientBalanceException : StateException
    {
        public InsufficientBalanceException(Address address, UInt256 transferAmount, UInt256 availableBalance)
            : base($"insufficient funds for transfer: address {address}, transfer amount {transferAmount}, available balance {availableBalance}")
        { }
    }
}
