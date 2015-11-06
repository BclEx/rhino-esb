using Rhino.Files;
using Rhino.ServiceBus.Impl;
using Rhino.ServiceBus.Internal;
using System;
using System.Transactions;

namespace Rhino.ServiceBus.RhinoFiles
{
    [CLSCompliant(false)]
    public class RhinoFilesOneWayBus : RhinoFilesTransport, IOnewayBus
    {
        private MessageOwnersSelector _messageOwners;
        public static readonly Uri NullEndpoint = new Uri(string.Format("null://nowhere:/middle"));

        public RhinoFilesOneWayBus(MessageOwner[] messageOwners, IMessageSerializer messageSerializer, string path, bool enablePerformanceCounters, IMessageBuilder<MessagePayload> messageBuilder, QueueManagerConfiguration queueManagerConfiguration)
            : base(NullEndpoint, new EndpointRouter(), messageSerializer, 1, path, IsolationLevel.ReadCommitted, 5, enablePerformanceCounters, messageBuilder, queueManagerConfiguration)
        {
            _messageOwners = new MessageOwnersSelector(messageOwners, new EndpointRouter());
            Start();
        }

        public void Send(params object[] msgs)
        {
            base.Send(_messageOwners.GetEndpointForMessageBatch(msgs), msgs);
        }
    }
}