// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Telegram.Plugin;

public class TelegramConfig : ITelegramConfig
{
    public bool Enabled { get; set; } = false;
    public string? AccessToken { get; set; }
    public string? LighthouseAuthKey { get; set; }
}
