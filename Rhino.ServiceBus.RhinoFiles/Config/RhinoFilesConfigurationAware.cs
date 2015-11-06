using Rhino.Files;
using Rhino.ServiceBus.Impl;
using Rhino.ServiceBus.Internal;
using Rhino.ServiceBus.RhinoFiles;
using System;
using System.Configuration;

namespace Rhino.ServiceBus.Config
{
    public class RhinoFilesConfigurationAware : IBusConfigurationAware
    {
        public void Configure(AbstractRhinoServiceBusConfiguration config, IBusContainerBuilder builder, IServiceLocator locator)
        {
            var busConfig = config as RhinoServiceBusConfiguration;
            if (busConfig == null)
                return;

            if (!config.Endpoint.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
                return;

            var busConfigSection = config.ConfigurationSection.Bus;

            if (string.IsNullOrEmpty(busConfigSection.Name))
                throw new ConfigurationErrorsException(
                    "Could not find attribute 'name' in node 'bus' in configuration");

            RegisterRhinoQueuesTransport(config, builder, locator);
        }

        private void RegisterRhinoQueuesTransport(AbstractRhinoServiceBusConfiguration c, IBusContainerBuilder b, IServiceLocator l)
        {
            var busConfig = c.ConfigurationSection.Bus;
            var fileManagerConfiguration = new QueueManagerConfiguration();

            b.RegisterSingleton<ISubscriptionStorage>(() => (ISubscriptionStorage)new FileSubscriptionStorage(
                busConfig.SubscriptionPath,
                l.Resolve<IMessageSerializer>(),
                l.Resolve<IReflection>()));

            b.RegisterSingleton<ITransport>(() => (ITransport)new RhinoFilesTransport(
                c.Endpoint,
                l.Resolve<IEndpointRouter>(),
                l.Resolve<IMessageSerializer>(),
                c.ThreadCount,
                busConfig.QueuePath,
                c.IsolationLevel,
                c.NumberOfRetries,
                busConfig.EnablePerformanceCounters,
                l.Resolve<IMessageBuilder<MessagePayload>>(),
                fileManagerConfiguration));

            b.RegisterSingleton<IMessageBuilder<MessagePayload>>(() => (IMessageBuilder<MessagePayload>)new RhinoFilesMessageBuilder(
                l.Resolve<IMessageSerializer>(),
                l.Resolve<IServiceLocator>()));

            b.RegisterSingleton<QueueManagerConfiguration>(() => fileManagerConfiguration);
        }
    }
}