// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;

namespace Nethermind.Api
{
    public class CheckHealthResult
    {
        public bool Healthy { get; set; }
        public IEnumerable<(string Message, string LongMessage)>? Messages { get; set; }
        public bool IsSyncing { get; set; }
        public IEnumerable<string>? Errors { get; set; }
    }

    public interface INodeHealthService
    {
        CheckHealthResult CheckHealth();

        bool CheckClAlive();

        IList<(string, double, double)> GetDiskSpaceInfo();
    }
}
