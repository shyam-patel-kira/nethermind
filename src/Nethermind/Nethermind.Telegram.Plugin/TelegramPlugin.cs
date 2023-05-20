using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Monitoring.Config;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nethermind.Telegram.Plugin;

public class TelegramPlugin : INethermindPlugin
{
    public string Name => "Telegram";
    public string Description => "Telegram notifications plugin";
    public string Author  => "Nethermind";

    private INethermindApi? _api;
    private ITelegramConfig? _config;
    private IMetricsConfig? _metricsConfig;

    private ILogger? _logger;

    private TelegramBotClient? _bot;

    private readonly ConcurrentDictionary<long, Address?> _chats = new();
    private readonly ConcurrentDictionary<Address, ConcurrentHashSet<long>> _trackedAddresses = new();
    private readonly ConcurrentDictionary<long, WaitFor> _waiting = new();

    enum WaitFor
    {
        GetAccount,
        Track
    }

    public Task Init(INethermindApi nethermindApi)
    {
        _api = nethermindApi;
        _config = nethermindApi.Config<ITelegramConfig>();
        _metricsConfig = nethermindApi.Config<IMetricsConfig>();
        _logger = nethermindApi.LogManager.GetClassLogger();

        if (_config.Enabled)
        {
            if (_config.AccessToken is null)
            {
                if (_logger.IsWarn) _logger.Warn("Access token is not configured");
                return Task.CompletedTask;
            }

            _bot = new TelegramBotClient(_config.AccessToken!);
        }

        return Task.CompletedTask;
    }

    public async Task InitNetworkProtocol()
    {
        if (!_config!.Enabled)
        {
            return;
        }

        User me = await _bot!.GetMeAsync();
        if (_logger!.IsInfo) _logger.Info($"Starting bot {me.Username}");

        ReceiverOptions receiverOptions = new ()
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot!.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions);

        AddressTracker tracker = new(Callback);

        _api!.BlockchainProcessor!.Tracers.Add(tracker);
    }

    private async Task<bool> HandleWaitingForMessage(ITelegramBotClient bot, long chatId, string text,
        CancellationToken cancellationToken)
    {
        if (_waiting.TryGetValue(chatId, out WaitFor waitingFor))
        {
            try
            {
                if (waitingFor == WaitFor.Track)
                {
                    Address address = new(text);
                    _chats[chatId] = address;
                    _trackedAddresses.GetOrAdd(address, v => new ConcurrentHashSet<long>()).Add(chatId);

                    await bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"Tracking address {address}",
                        cancellationToken: cancellationToken);

                }
                else if (waitingFor == WaitFor.GetAccount)
                {
                    Address address = new(text);
                    Keccak stateRoot = _api!.BlockTree!.BestSuggestedHeader!.StateRoot!;
                    Account? account = _api.StateReader!.GetAccount(stateRoot, address);

                    if (account is null)
                    {
                        await bot.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"Unknown account: {address}",
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"Balance: {account.Balance} Nonce: {account.Nonce}",
                            cancellationToken: cancellationToken);
                    }
                }

                await SendTextWithCommandButtons(bot, chatId, "Anything else?", cancellationToken);

                _waiting.TryRemove(chatId, out var _);
            }
            catch (Exception)
            {
                await bot.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Can't parse address {text}",
                    cancellationToken: cancellationToken);
            }
            return true;
        }

        return false;
    }

    async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if ((update.Message is null || update.Message.Text is null) && (update.CallbackQuery is null || update.CallbackQuery.Message is null))
        {
            _logger!.Warn($"Unknown message type {update.Type}");
            return;
        };

        Message message = update.Message ?? update.CallbackQuery!.Message!;
        string text = message.Text ?? update.CallbackQuery!.Data!;
        long chatId = message.Chat.Id;

        if (_logger!.IsWarn) _logger.Warn($"Received message. ChatId: {chatId} Text {text}");

        if (await HandleWaitingForMessage(bot, chatId, text, cancellationToken))
        {
            return;
        }

        if (text == "/start")
        {
            _chats[chatId] = null;

            await SendTextWithCommandButtons(bot, chatId, $"Tracking Nethermind node: {_metricsConfig!.NodeName}", cancellationToken);
        }
        else if (text == "/stop")
        {
            _chats.Remove(chatId, out Address? address);
            if (address is not null)
            {
                if (_trackedAddresses.TryGetValue(address, out ConcurrentHashSet<long>? chats))
                {
                    chats.TryRemove(chatId);
                }
                await SendTextWithCommandButtons(bot, chatId, $"Stopped tracking address: {address}", cancellationToken);
            }
            await SendTextWithCommandButtons(bot, chatId, $"Not tracking any address", cancellationToken);
        }
        else if (text == "/track")
        {
            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Enter address to track",
                cancellationToken: cancellationToken);
            _waiting[chatId] = WaitFor.Track;
        }
        else if (text == "/health")
        {
            await HandleHealth(bot, chatId, cancellationToken);
        }
        else if(text == "/status")
        {
            await HandleStatus(bot, chatId, cancellationToken);
        }
        else if (text == "/getAccount")
        {
            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: $"Enter address",
                cancellationToken: cancellationToken);
            _waiting[chatId] = WaitFor.GetAccount;
        }
        else
        {
            await SendTextWithCommandButtons(bot, chatId, $"Unknown command: {text}", cancellationToken);
        }
    }

    private static readonly InlineKeyboardButton _healthButton =
        InlineKeyboardButton.WithCallbackData("Node health", "/health");
    private static readonly InlineKeyboardButton _stopTrackingButton =
        InlineKeyboardButton.WithCallbackData("Stop tracking", "/stop");
    private static readonly InlineKeyboardButton _trackButton =
        InlineKeyboardButton.WithCallbackData("Track account", "/track");
    private static readonly InlineKeyboardButton _statusButton =
        InlineKeyboardButton.WithCallbackData("Node status", "/status");
    private static readonly InlineKeyboardButton _getAccountButton =
        InlineKeyboardButton.WithCallbackData("Get account data", "/getAccount");

    private static readonly InlineKeyboardButton[] _buttons = new[]
    {
        _healthButton, _stopTrackingButton, _trackButton, _statusButton, _getAccountButton
    };

    private static readonly InlineKeyboardMarkup _inlineKeyboard = new(_buttons);

    async Task SendTextWithCommandButtons(ITelegramBotClient bot, long chatId, string text, CancellationToken cancellationToken)
    {
        await bot.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            replyMarkup: _inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    async Task HandleStatus(ITelegramBotClient bot, long chatId, CancellationToken cancellationToken)
    {
        if (_api!.NodeHealthService is not null)
        {
            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: $"Head block: {_api.BlockTree!.BestSuggestedHeader!.ToString(BlockHeader.Format.Short)}",
                cancellationToken: cancellationToken);

            var diskSpaceInfos = _api.NodeHealthService.GetDiskSpaceInfo();

            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Available disk space:",
                cancellationToken: cancellationToken);

            foreach ((string dir, double space, double percentage) diskSpaceInfo in diskSpaceInfos)
            {
                await bot.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Disk \"{diskSpaceInfo.dir}\" available space: {diskSpaceInfo.space:F2}GB ({diskSpaceInfo.percentage:F2}%)",
                    cancellationToken: cancellationToken);
            }

            await SendTextWithCommandButtons(bot, chatId, "Anything else?", cancellationToken);
        }
    }

    async Task HandleHealth(ITelegramBotClient bot, long chatId, CancellationToken cancellationToken)
    {
        if (_api!.NodeHealthService is not null)
        {
            CheckHealthResult health = _api!.NodeHealthService.CheckHealth();

            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: $"Node health. Healthy: {health.Healthy} IsSyncing: {health.IsSyncing}",
                cancellationToken: cancellationToken);

            foreach ((string _, string longMessage) in health.Messages!)
            {
                await bot.SendTextMessageAsync(
                    chatId: chatId,
                    text: longMessage,
                    cancellationToken: cancellationToken);
            }

            await SendTextWithCommandButtons(bot, chatId, "Anything else?", cancellationToken);
        }
    }

    void Callback(Address from, Address to, UInt256 value)
    {
        if (_trackedAddresses.TryGetValue(from, out ConcurrentHashSet<long>? chats1))
        {
            foreach (long chatId in chats1)
            {
                _bot!.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Sent to: {to} value {value}");
            }
        }
        if(_trackedAddresses.TryGetValue(to, out ConcurrentHashSet<long>? chats2))
        {
            foreach (long chatId in chats2)
            {
                _bot!.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Received from: {from} value {value}");
            }
        }
    }

    Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        string errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"API Exception:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger!.Error(errorMessage);
        return Task.CompletedTask;
    }

    public Task InitRpcModules()
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
