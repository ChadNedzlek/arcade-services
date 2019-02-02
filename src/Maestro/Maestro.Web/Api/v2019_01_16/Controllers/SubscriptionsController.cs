// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.AspNetCore.ApiPagination;
using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.ServiceFabric.Actors;
using Swashbuckle.AspNetCore.Annotations;
using Channel = Maestro.Data.Models.Channel;
using Subscription = Maestro.Web.Api.v2019_01_16.Models.Subscription;
using SubscriptionUpdate = Maestro.Web.Api.v2018_07_16.Models.SubscriptionUpdate;
using SubscriptionData = Maestro.Web.Api.v2018_07_16.Models.SubscriptionData;

namespace Maestro.Web.Api.v2019_01_16.Controllers
{
    public class SubscriptionsController_ApiRemoved : Maestro.Web.Api.v2018_07_16.Controllers.SubscriptionsController
    {
        public SubscriptionsController_ApiRemoved(
            BuildAssetRegistryContext context,
            BackgroundQueue queue,
            IDependencyUpdater dependencyUpdater,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory)
            : base(context, queue, dependencyUpdater, subscriptionActorFactory)
        {
        }

        [ApiRemoved]
        public override sealed IActionResult GetAllSubscriptions(
            string sourceRepository = null,
            string targetRepository = null,
            int? channelId = null,
            bool? enabled = null)
        {
            throw new NotSupportedException();
        }

        [ApiRemoved]
        public override Task<IActionResult> GetSubscription(Guid id)
        {
            throw new NotSupportedException();
        }

        [ApiRemoved]
        public override Task<IActionResult> TriggerSubscription(Guid id)
        {
            throw new NotSupportedException();
        }

        [ApiRemoved]
        public override Task<IActionResult> UpdateSubscription(Guid id, [FromBody] SubscriptionUpdate update)
        {
            throw new NotSupportedException();
        }

        [ApiRemoved]
        public override Task<IActionResult> DeleteSubscription(Guid id)
        {
            throw new NotSupportedException();
        }

        [ApiRemoved]
        public override Task<IActionResult> Create([FromBody] SubscriptionData subscription)
        {
            throw new NotSupportedException();
        }
    }

    [Route("subscriptions")]
    [ApiVersion("2019-01-16")]
    public class SubscriptionsController : SubscriptionsController_ApiRemoved
    {
        private readonly BuildAssetRegistryContext _context;
        private readonly BackgroundQueue _queue;
        private readonly IDependencyUpdater _dependencyUpdater;
        private readonly Func<ActorId, ISubscriptionActor> _subscriptionActorFactory;

        public SubscriptionsController(
            BuildAssetRegistryContext context,
            BackgroundQueue queue,
            IDependencyUpdater dependencyUpdater,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory)
            : base(context, queue, dependencyUpdater, subscriptionActorFactory)
        {
            _context = context;
            _queue = queue;
            _dependencyUpdater = dependencyUpdater;
            _subscriptionActorFactory = subscriptionActorFactory;
        }

        [HttpGet]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(List<Subscription>))]
        [ValidateModelState]
        public new IActionResult GetAllSubscriptions(
            string sourceRepository = null,
            string targetRepository = null,
            int? channelId = null,
            bool? enabled = null)
        {
            IQueryable<Data.Models.Subscription> query = _context.Subscriptions.Include(s => s.Channel);

            if (!string.IsNullOrEmpty(sourceRepository))
            {
                query = query.Where(sub => sub.SourceRepository == sourceRepository);
            }

            if (!string.IsNullOrEmpty(targetRepository))
            {
                query = query.Where(sub => sub.TargetRepository == targetRepository);
            }

            if (channelId.HasValue)
            {
                query = query.Where(sub => sub.ChannelId == channelId.Value);
            }

            if (enabled.HasValue)
            {
                query = query.Where(sub => sub.Enabled == enabled.Value);
            }

            List<Subscription> results = query.AsEnumerable().Select(sub => new Subscription(sub)).ToList();
            return Ok(results);
        }

        [HttpGet("{id}")]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(Subscription))]
        [ValidateModelState]
        public new async Task<IActionResult> GetSubscription(Guid id)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Include(sub => sub.LastAppliedBuild)
                .Include(sub => sub.Channel)
                .FirstOrDefaultAsync(sub => sub.Id == id);

            if (subscription == null)
            {
                return NotFound();
            }

            return Ok(new Subscription(subscription));
        }

        /// <summary>
        ///     Trigger a subscription manually by ID
        /// </summary>
        /// <param name="id">ID of subscription</param>
        [HttpPost("{id}/trigger")]
        [SwaggerResponse((int)HttpStatusCode.Accepted, Type = typeof(Subscription))]
        [ValidateModelState]
        public new async Task<IActionResult> TriggerSubscription(Guid id)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Include(sub => sub.LastAppliedBuild)
                .Include(sub => sub.Channel)
                .FirstOrDefaultAsync(sub => sub.Id == id);

            if (subscription == null)
            {
                return NotFound();
            }

            _queue.Post(
                async () =>
                {
                    await _dependencyUpdater.StartSubscriptionUpdateAsync(id);
                });

            return Accepted(new Subscription(subscription));
        }

        [HttpPatch("{id}")]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(Subscription))]
        [ValidateModelState]
        public new async Task<IActionResult> UpdateSubscription(Guid id, [FromBody] SubscriptionUpdate update)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Where(sub => sub.Id == id)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return NotFound();
            }

            var doUpdate = false;

            if (!string.IsNullOrEmpty(update.SourceRepository))
            {
                subscription.SourceRepository = update.SourceRepository;
                doUpdate = true;
            }

            if (update.Policy != null)
            {
                subscription.PolicyObject = update.Policy.ToDb();
                doUpdate = true;
            }

            if (!string.IsNullOrEmpty(update.ChannelName))
            {
                Channel channel = await _context.Channels.Where(c => c.Name == update.ChannelName)
                    .FirstOrDefaultAsync();
                if (channel == null)
                {
                    return BadRequest(
                        new ApiError(
                            "The request is invalid",
                            new[] { $"The channel '{update.ChannelName}' could not be found." }));
                }

                subscription.Channel = channel;
                doUpdate = true;
            }

            if (update.Enabled.HasValue)
            {
                subscription.Enabled = update.Enabled.Value;
                doUpdate = true;
            }

            if (doUpdate)
            {
                _context.Subscriptions.Update(subscription);
                await _context.SaveChangesAsync();
            }


            return Ok(new Subscription(subscription));
        }

        [HttpDelete("{id}")]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(Subscription))]
        [ValidateModelState]
        public new async Task<IActionResult> DeleteSubscription(Guid id)
        {
            Data.Models.Subscription subscription =
                await _context.Subscriptions.FirstOrDefaultAsync(sub => sub.Id == id);

            if (subscription == null)
            {
                return NotFound();
            }

            Data.Models.SubscriptionUpdate subscriptionUpdate =
                await _context.SubscriptionUpdates.FirstOrDefaultAsync(u => u.SubscriptionId == subscription.Id);

            if (subscriptionUpdate != null)
            {
                _context.SubscriptionUpdates.Remove(subscriptionUpdate);
            }

            _context.Subscriptions.Remove(subscription);

            await _context.SaveChangesAsync();
            return Ok(new Subscription(subscription));
        }

        [HttpPost]
        [SwaggerResponse((int)HttpStatusCode.Created, Type = typeof(Subscription))]
        [ValidateModelState]
        public new async Task<IActionResult> Create([FromBody] SubscriptionData subscription)
        {
            Channel channel = await _context.Channels.Where(c => c.Name == subscription.ChannelName)
                .FirstOrDefaultAsync();
            if (channel == null)
            {
                return BadRequest(
                    new ApiError(
                        "the request is invalid",
                        new[] { $"The channel '{subscription.ChannelName}' could not be found." }));
            }

            Repository repo = await _context.Repositories.FindAsync(subscription.TargetRepository);

            if (subscription.TargetRepository.Contains("github.com"))
            {
                // If we have no repository information or an invalid installation id
                // then we will fail when trying to update things, so we fail early.
                if (repo == null || repo.InstallationId <= 0)
                {
                    return BadRequest(
                        new ApiError(
                            "the request is invalid",
                            new[]
                            {
                                $"The repository '{subscription.TargetRepository}' does not have an associated github installation. " +
                                "The Maestro github application must be installed by the repository's owner and given access to the repository."
                            }));
                }
            }
            // In the case of a dev.azure.com repository, we don't have an app installation,
            // but we should add an entry in the repositories table, as this is required when
            // adding a new subscription policy.
            // NOTE:
            // There is a good chance here that we will need to also handle <account>.visualstudio.com
            // but leaving it out for now as it would be preferred to use the new format
            else if (subscription.TargetRepository.Contains("dev.azure.com"))
            {
                if (repo == null)
                {
                    _context.Repositories.Add(
                        new Data.Models.Repository
                        {
                            RepositoryName = subscription.TargetRepository,
                            InstallationId = default
                        });
                }
            }

            Data.Models.Subscription subscriptionModel = subscription.ToDb();
            subscriptionModel.Channel = channel;
            await _context.Subscriptions.AddAsync(subscriptionModel);
            await _context.SaveChangesAsync();
            return CreatedAtRoute(
                new
                {
                    action = "GetSubscription",
                    id = subscriptionModel.Id
                },
                new Subscription(subscriptionModel));
        }
    }
}