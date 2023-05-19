// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;

namespace Nethermind.Telegram.Plugin;

public interface ITelegramConfig : IConfig
{
    [ConfigItem(
        Description = "Defines whether the Telegram plugin is enabled.",
        DefaultValue = "false")]
    bool Enabled { get; set; }

    [ConfigItem(
        Description = "Your telegram bot access token",
        DefaultValue = "null")]
    string? AccessToken { get; set; }
}
