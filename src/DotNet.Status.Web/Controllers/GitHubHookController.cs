// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebHooks;
using Microsoft.Extensions.Logging;
using Octokit;

namespace DotNet.Status.Web.Controllers
{
    public class GitHubHookController : ControllerBase
    {
        private readonly ILogger<GitHubHookController> _logger;
        private readonly IEnumerable<IIssueProcessor> _issueProcessors;
        private readonly IEnumerable<IPullRequestProcessor> _pullRequestProcessors;

        public GitHubHookController(
            IEnumerable<IIssueProcessor> issueProcessors,
            IEnumerable<IPullRequestProcessor> pullRequestProcessors,
            ILogger<GitHubHookController> logger)
        {
            _logger = logger;
            _issueProcessors = issueProcessors;
            _pullRequestProcessors = pullRequestProcessors;
        }

        [GitHubWebHook(EventName = "issues")]
        public async Task<IActionResult> IssuesHook(JsonElement data)
        {
            // because system.text.json default serialization setting can't deser web hook json payload we need custom JsonSerializerOptions
            // just for this controller. see https://github.com/dotnet/core-eng/issues/10378
            var issueEvent = JsonSerializer.Deserialize<IssuesHookData>(data.ToString(), SerializerOptions());

            _logger.LogInformation("Processing issues action '{action}' for issue {repo}/{number}", issueEvent.Action, issueEvent.Repository.Name, issueEvent.Issue.Number);

            await Task.WhenAll(_issueProcessors.Select(ip => ip.ProcessIssueEvent(issueEvent)));

            return NoContent();
        }

        [GitHubWebHook(EventName = "pull_request")]
        public async Task<IActionResult> PullRequestHook(JsonElement data)
        {
            // because system.text.json default serialization setting can't deser web hook json payload we need custom JsonSerializerOptions
            // just for this controller. see https://github.com/dotnet/core-eng/issues/10378
            var pullRequestEvent = JsonSerializer.Deserialize<PullRequestHookData>(data.ToString(), SerializerOptions());

            _logger.LogInformation("Processing pull request action '{action}' for issue {repo}/{number}", pullRequestEvent.Action, pullRequestEvent.Repository.Name, pullRequestEvent.Number);

            await Task.WhenAll(_pullRequestProcessors.Select(ip => ip.ProcessPullRequestEvent(pullRequestEvent)));

            return NoContent();
        }

        public static JsonSerializerOptions SerializerOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            return options;
        }

        [GitHubWebHook]
        public IActionResult AcceptHook()
        {
            // Ignore them, none are interesting
            return NoContent();
        }
    }
}
