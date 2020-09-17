// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DotNet.Status.Web
{
    public interface IPullRequestProcessor
    {
        Task ProcessPullRequestEvent(PullRequestHookData payload);
    }

    public class PullRequestHookData
    {
        public string Action { get; set; }
        [JsonPropertyName("pull_request")]
        [Newtonsoft.Json.JsonProperty("pull_request")]
        public int HtmlUrl { get; set; }
        public string Number { get; set; }
        public PullRequestHookPullRequest PullRequest { get; set; }
        public HookRepository Repository { get; set; }
        public HookUser Sender { get; set; }
    }

    public class PullRequestHookPullRequest
    {
        public bool Merged { get; set; }
        public HookUser User { get; set; }
        public PullRequestGitReference Head { get; set; }
        public PullRequestGitReference Base { get; set; }
        [JsonPropertyName("merged_at")]
        [Newtonsoft.Json.JsonProperty("merged_at")]
        public string MergedAt { get; }
    }

    public class PullRequestGitReference
    {
        public string Sha { get; set; }
        public string Ref { get; set; }
    }
}
