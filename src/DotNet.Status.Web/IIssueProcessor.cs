// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Octokit;

namespace DotNet.Status.Web
{
    public interface IIssueProcessor
    {
        Task ProcessIssueEvent(IssuesHookData issuePayload);
    }

    public class IssuesHookData
    {
        public string Action { get; set; }
        public IssuesHookIssue Issue { get; set; }
        public HookUser Sender { get; set; }
        public HookRepository Repository { get; set; }
        public IssuesHookLabel Label { get; set; }
    }

    public class HookRepository
    {
        public string Name { get; set; }
        public HookUser Owner { get; set; }
        public long Id { get; set; }
    }

    public class IssuesHookIssue
    {
        public int Number { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public HookUser Assignee { get; set; }
        public ImmutableArray<IssuesHookLabel> Labels { get; set; }
        public ItemState State { get; set; }
        public string Url { get; set; }
        [JsonPropertyName("html_url")]
        [Newtonsoft.Json.JsonProperty("html_url")]
        public string HtmlUrl { get; set; }
    }

    public class IssuesHookLabel
    {
        public string Name { get; set; }
    }

    public class HookUser
    {
        public string Login { get; set; }
    }
}
