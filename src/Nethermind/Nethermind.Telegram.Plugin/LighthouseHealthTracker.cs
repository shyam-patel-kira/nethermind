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

            var response = _jsonSerializer.Deserialize<ValidatorsJsonResponse>(await content.ReadAsStringAsync());
            List<string> result = new();
            foreach (ValidatorInfo validatorInfo in response.Data)
            {
                result.Add($"Validator pub key:\n{validatorInfo.VotingPubkey}\nEnabled: {validatorInfo.Enabled}");
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
        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; }

        [JsonProperty(PropertyName = "voting_pubkey")]
        public string VotingPubkey { get; set; } = string.Empty;
    }

    private class ValidatorsJsonResponse
    {
        [JsonProperty(PropertyName = "data")]
        public ValidatorInfo[] Data { get; set; } = Array.Empty<ValidatorInfo>();
    }
}
