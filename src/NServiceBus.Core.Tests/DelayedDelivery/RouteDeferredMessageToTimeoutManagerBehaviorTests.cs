﻿namespace NServiceBus.Core.Tests.Timeout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.DelayedDelivery;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;
    using NUnit.Framework;
    using Testing;

    class RouteDeferredMessageToTimeoutManagerBehaviorTests
    {
        [Test]
        public async Task Should_reroute_to_the_timeout_manager()
        {
            var behavior = new RouteDeferredMessageToTimeoutManagerBehavior("tm");
            var delay = TimeSpan.FromDays(1);

            var headers = new Dictionary<string, string>();
            string destination = null;
            var context = CreateContext(new UnicastRoutingStrategy("target"), new DelayDeliveryWith(delay));
        
            await behavior.Invoke(context, c =>
            {
                var addressTag = (UnicastAddressTag) c.RoutingStrategies.First().Apply(headers);
                destination = addressTag.Destination;
                return TaskEx.CompletedTask;
            });

            Assert.AreEqual("tm", destination);
            Assert.AreEqual(headers[TimeoutManagerHeaders.RouteExpiredTimeoutTo], "target");
        }

        [Test]
        public void Supports_only_unicast_routing()
        {
            var behavior = new RouteDeferredMessageToTimeoutManagerBehavior("tm");
            var delay = TimeSpan.FromDays(1);

            var context = CreateContext(new MulticastRoutingStrategy(null), new DelayDeliveryWith(delay));
        
            Assert.That(async () => await behavior.Invoke(context, () => TaskEx.CompletedTask), Throws.InstanceOf<Exception>().And.Message.Contains("Delayed delivery using the Timeout Manager is only supported for messages with unicast routing"));
        }

        [Test]
        public void Cannot_be_combined_with_time_to_be_received()
        {
            var behavior = new RouteDeferredMessageToTimeoutManagerBehavior("tm");
            var delay = TimeSpan.FromDays(1);

            var context = CreateContext(new UnicastRoutingStrategy("target"), new DelayDeliveryWith(delay), new DiscardIfNotReceivedBefore(TimeSpan.FromSeconds(30)));

            Assert.That(async () => await behavior.Invoke(context, () => TaskEx.CompletedTask), Throws.InstanceOf<Exception>().And.Message.Contains("Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of this type."));
        }

        [Test]
        public async Task Should_set_the_expiry_header_to_a_absolute_utc_time_calculated_based_on_delay()
        {
            var behavior = new RouteDeferredMessageToTimeoutManagerBehavior("tm");
            var delay = TimeSpan.FromDays(1);
            
            var headers = new Dictionary<string, string>();
            var context = CreateContext(new UnicastRoutingStrategy("target"), new DelayDeliveryWith(delay));
        
            await behavior.Invoke(context, c =>
            {
                c.RoutingStrategies.First().Apply(headers);
                return TaskEx.CompletedTask;
            });

            Assert.LessOrEqual(DateTimeExtensions.ToUtcDateTime(headers[TimeoutManagerHeaders.Expire]), DateTime.UtcNow + delay);
        }

        [Test]
        public async Task Should_set_the_expiry_header_to_a_absolute_utc_time()
        {
            var behavior = new RouteDeferredMessageToTimeoutManagerBehavior("tm");
            var at = DateTime.UtcNow + TimeSpan.FromDays(1);
            
            var headers = new Dictionary<string, string>();
            var context = CreateContext(new UnicastRoutingStrategy("target"), new DoNotDeliverBefore(at));

            await behavior.Invoke(context, c =>
            {
                c.RoutingStrategies.First().Apply(headers);
                return TaskEx.CompletedTask;
            });

            Assert.AreEqual(headers[TimeoutManagerHeaders.Expire], DateTimeExtensions.ToWireFormattedString(at));
        }

        TestableRoutingContext CreateContext(RoutingStrategy routingStrategy, params DeliveryConstraint[] deliveryConstraints)
        {
            var context = new TestableRoutingContext
            {
                RoutingStrategies = new List<RoutingStrategy>
                {
                    routingStrategy
                }
            };
            foreach (var deliveryContraint in deliveryConstraints)
            {
                context.Extensions.AddDeliveryConstraint(deliveryContraint);
            }

            return context;
        }
    }
}