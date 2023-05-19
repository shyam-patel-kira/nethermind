using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Monitoring.Config;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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

    private ConcurrentDictionary<long, Address?> _chats = new();
    private ConcurrentDictionary<Address, long> _trackedAddresses = new();

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

    async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is null || update.Message.Text is null) return;

        Message message = update.Message;
        string text = message.Text;

        long chatId = message.Chat.Id;

        if (text == "/start")
        {
            _chats[chatId] = null;
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Tracking Nethermind node: {_metricsConfig!.NodeName}",
                cancellationToken: cancellationToken);
        }
        else if (text == "/stop")
        {
            _chats.Remove(chatId, out Address? address);
            if (address is not null)
            {
                _trackedAddresses.Remove(address, out long _);
            }
        } else if (text == "/track")
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Enter address to track",
                cancellationToken: cancellationToken);
        }
        else if (text == "/health")
        {
            if (_api!.NodeHealthService is not null)
            {
                CheckHealthResult health = _api!.NodeHealthService.CheckHealth();

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Node health. Healthy: {health.Healthy} IsSyncing: {health.IsSyncing}",
                    cancellationToken: cancellationToken);

                foreach ((string _, string longMessage) in health.Messages!)
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: longMessage,
                        cancellationToken: cancellationToken);
                }
            }
        }
        else
        {
            try
            {
                Address address = new(text);
                _chats[chatId] = address;
                _trackedAddresses[address] = chatId;
            }
            catch (Exception)
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Unknown command {text}",
                    cancellationToken: cancellationToken);
            }
        }
    }

    void Callback(Address from, Address to, UInt256 value)
    {
        if (_trackedAddresses.TryGetValue(from, out long chatId1))
        {
            _bot!.SendTextMessageAsync(
                chatId: chatId1,
                text: $"Send to: {to} value {value}");
        }
        else if(_trackedAddresses.TryGetValue(to, out long chatId2))
        {
            _bot!.SendTextMessageAsync(
                chatId: chatId2,
                text: $"Received from: {from} value {value}");
        }
    }

    Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger!.Error(ErrorMessage);
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

