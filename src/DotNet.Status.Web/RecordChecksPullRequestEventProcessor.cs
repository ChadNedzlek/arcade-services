// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using Kusto.Ingest;
using Microsoft.DotNet.GitHub.Authentication;
using Microsoft.DotNet.Kusto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace DotNet.Status.Web
{
    public class RecordChecksPullRequestProcessor : IPullRequestProcessor
    {
        private readonly IKustoIngestClientFactory _kustoFactory;
        private readonly IGitHubApplicationClientFactory _gitHubApplicationClientFactory;
        private readonly IOptions<KustoOptions> _kustoOptions;
        private readonly ILogger<RecordChecksPullRequestProcessor> _logger;

        public RecordChecksPullRequestProcessor(
            IKustoIngestClientFactory kustoFactory,
            IGitHubApplicationClientFactory gitHubApplicationClientFactory,
            IOptions<KustoOptions> kustoOptions,
            ILogger<RecordChecksPullRequestProcessor> logger)
        {
            _kustoFactory = kustoFactory;
            _gitHubApplicationClientFactory = gitHubApplicationClientFactory;
            _kustoOptions = kustoOptions;
            _logger = logger;
        }

        public Task ProcessPullRequestEvent(PullRequestHookData payload)
        {
            if (payload.Action != "closed" ||
                !payload.PullRequest.Merged)
            {
                // This isn't a "merge the pull request" action, just leave, without logging, because this happens a lot
                return Task.CompletedTask;
            }

            return RecordPullRequestMerge(payload);
        }

        private async Task RecordPullRequestMerge(PullRequestHookData payload)
        {
            using IKustoIngestClient kustoClient = _kustoFactory.GetClient();

            IGitHubClient gitHubClient = await _gitHubApplicationClientFactory.CreateGitHubClientAsync(
                payload.Repository.Owner.Login,
                payload.Repository.Name
            );

            CheckRunsResponse checks = await gitHubClient.Check.Run.GetAllForReference(payload.Repository.Id, payload.PullRequest.Head.Sha);

            bool allSucceeded = checks.CheckRuns.All(c => c.Conclusion?.Value == CheckConclusion.Success);

            await KustoHelpers.WriteDataToKustoInMemoryAsync(
                kustoClient,
                _kustoOptions.Value.Database,
                "GitHubPullRequests",
                _logger,
                new[] {payload},
                p => new[]
                {
                    new KustoValue("Owner", p.Repository.Owner, KustoDataType.String),
                    new KustoValue("Repository", p.Repository.Name, KustoDataType.String),
                    new KustoValue("PullRequestNumber", p.Number, KustoDataType.Int),
                    new KustoValue("Branch", p.PullRequest?.Base?.Ref, KustoDataType.String),
                    new KustoValue("MergedCommit", p.PullRequest?.Head?.Sha, KustoDataType.String),
                    new KustoValue("MergedBy", p.Sender?.Login, KustoDataType.String),
                    new KustoValue("CreatedBy", p.PullRequest?.User?.Login, KustoDataType.String),
                    new KustoValue("MergedAt", p.PullRequest?.MergedAt, KustoDataType.DateTime),
                    new KustoValue("ChecksPassed", allSucceeded, KustoDataType.Boolean),
                }
            );

            await KustoHelpers.WriteDataToKustoInMemoryAsync(
                kustoClient,
                _kustoOptions.Value.Database,
                "GitHubChecks",
                _logger,
                checks.CheckRuns,
                c => new[]
                {
                    new KustoValue("CheckCommit", c.HeadSha, KustoDataType.String),
                    new KustoValue("Conclusion", c.Conclusion?.StringValue ?? "None", KustoDataType.String),
                    new KustoValue("Name", c.Name, KustoDataType.String),
                    new KustoValue("StartedAt", c.StartedAt, KustoDataType.DateTime),
                    new KustoValue("ConcludedAt", c.CompletedAt, KustoDataType.DateTime),
                    new KustoValue("Status", c.Status.StringValue, KustoDataType.String),
                }
            );
        }
    }
}
