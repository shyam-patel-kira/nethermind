// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nethermind.Telegram.Plugin;

public class LighthouseHealthTracker
{
    private readonly string _authKey;

    public LighthouseHealthTracker(string authKey)
    {
        _authKey = authKey;
    }

    public async Task<string> GetValidatorInfo()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("accept", "application/json");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer: {_authKey}");
        HttpContent content = (await client.GetAsync("http://localhost:5062/lighthouse/validators")).Content;
        return await content.ReadAsStringAsync();
    }
}
