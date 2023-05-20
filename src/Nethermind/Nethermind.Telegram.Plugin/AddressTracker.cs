// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using ConcurrentCollections;
using Nethermind.Core;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Telegram.Bot;

namespace Nethermind.Telegram.Plugin;

public class AddressTracker : IBlockTracer
{
    private readonly ITelegramBotClient _bot;

    private readonly ConcurrentDictionary<long, Address> _chats = new();
    private readonly ConcurrentDictionary<Address, ConcurrentHashSet<long>> _trackedAddresses = new();

    public AddressTracker(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public void TrackAddress(Address address, long chatId)
    {
        _chats[chatId] = address;
        _trackedAddresses.GetOrAdd(address, v => new ConcurrentHashSet<long>()).Add(chatId);
    }

    public Address? StopTracking(long chatId)
    {
        if (_chats.TryRemove(chatId, out Address? address))
        {
            if (_trackedAddresses.TryGetValue(address, out ConcurrentHashSet<long>? chats))
            {
                chats.TryRemove(chatId);
            }

            return address;
        }

        return null;
    }

    public ITxTracer StartNewTxTrace(Transaction? tx)
    {
        if (tx is not null && tx.SenderAddress is not null && tx.To is not null)
        {
            Address from = tx.SenderAddress;
            Address to = tx.To;
            UInt256 value = tx.Value;
            if (_trackedAddresses.TryGetValue(from, out ConcurrentHashSet<long>? chats1))
            {
                foreach (long chatId in chats1)
                {
                    _bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"Sent to: {to} value {value}");
                }
            }

            if (_trackedAddresses.TryGetValue(to, out ConcurrentHashSet<long>? chats2))
            {
                foreach (long chatId in chats2)
                {
                    _bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"Received from: {from} value {value}");
                }
            }
        }
        return NullTxTracer.Instance;
    }

    public bool IsTracingRewards => false;
    public void EndTxTrace() { }
    public void EndBlockTrace() { }
    public void ReportReward(Address author, string rewardType, UInt256 rewardValue) { }
    public void StartNewBlockTrace(Block block) { }
}
