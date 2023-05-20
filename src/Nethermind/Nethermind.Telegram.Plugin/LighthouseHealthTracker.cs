// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Nethermind.Serialization.Json;
using Newtonsoft.Json;

namespace Nethermind.Telegram.Plugin;

public class LighthouseHealthTracker
{
    private readonly string _authKey;
    private readonly IJsonSerializer _jsonSerializer;

    public LighthouseHealthTracker(string authKey, IJsonSerializer jsonSerializer)
    {
        _authKey = authKey;
        _jsonSerializer = jsonSerializer;
    }

    public async Task<List<string>?> GetValidatorInfo()
    {
        try
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_authKey}");
            HttpContent content = (await client.GetAsync("http://localhost:5062/lighthouse/validators")).Content;

            var response = _jsonSerializer.Deserialize<JsonResponse<ValidatorKeyAndEnabled[]>>(await content.ReadAsStringAsync());
            List<string> result = new();
            foreach (ValidatorKeyAndEnabled key in response.Data!)
            {
                content = (await client.GetAsync(
                        $"http://localhost:5052/eth/v1/beacon/states/head/validators/{key.VotingPubkey}"))
                    .Content;

                var info =
                    _jsonSerializer.Deserialize<JsonResponse<ValidatorInfo>>(await content.ReadAsStringAsync()).Data!;

                result.Add(
                    $"Validator pub key:\n{key.VotingPubkey}\nIndex: {info.Index}\nStatus: {info.Status}\nBalance: {info.Balance} GWei");
            }

            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private class ValidatorInfo
    {
        [JsonProperty(PropertyName = "index")]
        public string Index { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "balance")]
        public string Balance { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; } = string.Empty;
    }

    private class ValidatorKeyAndEnabled
    {
        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; }

        [JsonProperty(PropertyName = "voting_pubkey")]
        public string VotingPubkey { get; set; } = string.Empty;
    }

    private class JsonResponse<T>
    {
        [JsonProperty(PropertyName = "data")]
        public T? Data { get; set; }
    }
}
