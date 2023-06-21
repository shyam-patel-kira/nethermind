// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Newtonsoft.Json;

namespace Nethermind.Merge.Plugin.Data;

/// <summary>
/// Represents an object mapping the <c>ExecutionPayloadV3</c> structure of the beacon chain spec.
/// </summary>
[JsonObject(ItemRequired = Required.Always)]
public class ExecutionPayloadV3 : ExecutionPayload
{
    public ExecutionPayloadV3() { } // Needed for tests

    public ExecutionPayloadV3(Block block) : base(block)
    {
    }
}
