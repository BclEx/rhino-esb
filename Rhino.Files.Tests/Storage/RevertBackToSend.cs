﻿using Rhino.Files.Storage;
using System;
using System.Collections.Specialized;
using System.IO;
using Xunit;

namespace Rhino.Files.Tests.Storage
{
    public class RevertBackToSend
    {
        public RevertBackToSend()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
        }

        [Fact]
        public void MovesMessageToOutgoingFromHistory()
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
                    Headers = new NameValueCollection(),
                };
                qf.Global(actions =>
                {
                    var transactionId = Guid.NewGuid();
                    actions.RegisterToSend("localhost", "test", null, testMessage, transactionId);
                    actions.MarkAsReadyToSend(transactionId);
                    actions.Commit();
                });
                qf.Send(actions =>
                {
                    string endpoint;
                    var msgs = actions.GetMessagesToSendAndMarkThemAsInFlight(int.MaxValue, int.MaxValue, out endpoint);
                    var bookmark = actions.MarkOutgoingMessageAsSuccessfullySent(msgs[0].Bookmark);
                    actions.RevertBackToSend(new[] { bookmark });
                    msgs = actions.GetMessagesToSendAndMarkThemAsInFlight(int.MaxValue, int.MaxValue, out endpoint);
                    Assert.NotEmpty(msgs);
                    actions.Commit();
                });
                qf.Global(actions =>
                {
                    var messages = actions.GetSentMessages();
                    Assert.Empty(messages);
                    actions.Commit();
                });
            }
        }
    }
}