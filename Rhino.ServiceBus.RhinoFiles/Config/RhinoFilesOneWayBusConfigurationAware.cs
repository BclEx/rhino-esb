using Rhino.Files;
using Rhino.ServiceBus.Impl;
using Rhino.ServiceBus.Internal;
using Rhino.ServiceBus.RhinoFiles;
using System;
using System.Collections.Generic;

namespace Rhino.ServiceBus.Config
{
    public class RhinoFilesOneWayBusConfigurationAware : IBusConfigurationAware
    {
        public void Configure(AbstractRhinoServiceBusConfiguration config, IBusContainerBuilder builder, IServiceLocator locator)
        {
            var oneWayConfig = config as OnewayRhinoServiceBusConfiguration;
            if (oneWayConfig == null)
                return;

            var messageOwners = new List<MessageOwner>();
            var messageOwnersReader = new MessageOwnersConfigReader(config.ConfigurationSection, messageOwners);
            messageOwnersReader.ReadMessageOwners();

            if (!messageOwnersReader.EndpointScheme.Equals("rhino.queues", StringComparison.InvariantCultureIgnoreCase))
                return;

            oneWayConfig.MessageOwners = messageOwners.ToArray();
            RegisterRhinoQueuesOneWay(config, builder, locator);
        }

        private void RegisterRhinoQueuesOneWay(AbstractRhinoServiceBusConfiguration c, IBusContainerBuilder b, IServiceLocator l)
        {
            var oneWayConfig = (OnewayRhinoServiceBusConfiguration)c;
            var busConfig = c.ConfigurationSection.Bus;
            var queueManagerConfiguration = new QueueManagerConfiguration();

            b.RegisterSingleton<IMessageBuilder<MessagePayload>>(() => (IMessageBuilder<MessagePayload>)new RhinoFilesMessageBuilder(
                l.Resolve<IMessageSerializer>(),
                l.Resolve<IServiceLocator>()));

            b.RegisterSingleton<IOnewayBus>(() => (IOnewayBus)new RhinoFilesOneWayBus(
                oneWayConfig.MessageOwners,
                l.Resolve<IMessageSerializer>(),
                busConfig.QueuePath,
                busConfig.EnablePerformanceCounters,
                l.Resolve<IMessageBuilder<MessagePayload>>(),
                queueManagerConfiguration));

            b.RegisterSingleton<QueueManagerConfiguration>(() => queueManagerConfiguration);
        }
    }
}