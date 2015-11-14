using Rhino.Files.Storage;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Xunit;

namespace Rhino.Files.Tests.Storage
{
    public class DeliveryOptions
    {
        public DeliveryOptions()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
        }

        [Fact]
        public void MovesExpiredMessageToOutgoingHistory()
        {
            using (var qf = new QueueStorage("test.esent", new QueueManagerConfiguration()))
            {
                qf.Initialize();
                qf.Global(actions =>
                {
                    actions.CreateQueueIfDoesNotExists("test");
                    actions.Commit();
                });
                var testMessage = new MessagePayload
                {
                    Data = new byte[0],
                    DeliverBy = DateTime.Now.AddSeconds(-1),
                    Headers = new NameValueCollection(),
                    MaxAttempts = null
                };
                var messageId = Guid.Empty;
                qf.Global(actions =>
                {
                    var transactionId = Guid.NewGuid();
                    messageId = actions.RegisterToSend("localhost", "test", null, testMessage, transactionId);
                    actions.MarkAsReadyToSend(transactionId);
                    actions.Commit();
                });
                qf.Send(actions =>
                {
                    string endpoint;
                    var msgs = actions.GetMessagesToSendAndMarkThemAsInFlight(int.MaxValue, int.MaxValue, out endpoint);
                    Assert.Empty(msgs);
                    actions.Commit();
                });
                qf.Global(actions =>
                {
                    var message = actions.GetSentMessages().FirstOrDefault(x => x.Id.MessageIdentifier == messageId);
                    Assert.NotNull(message);
                    Assert.Equal(OutgoingMessageStatus.Failed, message.OutgoingStatus);
                    actions.Commit();
                });
            }
        }

        [Fact]
        public void MovesMessageToOutgoingHistoryAfterMaxAttempts()
        {
            using (var qf = new QueueStorage("test.esent", new QueueManagerConfiguration()))
            {
                qf.Initialize();
                qf.Global(actions =>
                {
                    actions.CreateQueueIfDoesNotExists("test");
                    actions.Commit();
                });
                var testMessage = new MessagePayload
                {
                    Data = new byte[0],
                    DeliverBy = null,
                    Headers = new NameValueCollection(),
                    MaxAttempts = 1
                };
                var messageId = Guid.Empty;
                qf.Global(actions =>
                {
                    var transactionId = Guid.NewGuid();
                    messageId = actions.RegisterToSend("localhost", "test", null, testMessage, transactionId);
                    actions.MarkAsReadyToSend(transactionId);
                    actions.Commit();
                });
                qf.Send(actions =>
                {
                    string endpoint;
                    var msgs = actions.GetMessagesToSendAndMarkThemAsInFlight(int.MaxValue, int.MaxValue, out endpoint);
                    actions.MarkOutgoingMessageAsFailedTransmission(msgs.First().Bookmark, false);
                    msgs = actions.GetMessagesToSendAndMarkThemAsInFlight(int.MaxValue, int.MaxValue, out endpoint);
                    Assert.Empty(msgs);
                    actions.Commit();
                });
                qf.Global(actions =>
                {
                    var message = actions.GetSentMessages().FirstOrDefault(x => x.Id.MessageIdentifier == messageId);
                    Assert.NotNull(message);
                    Assert.Equal(OutgoingMessageStatus.Failed, message.OutgoingStatus);
                    actions.Commit();
                });
            }
        }
    }
}