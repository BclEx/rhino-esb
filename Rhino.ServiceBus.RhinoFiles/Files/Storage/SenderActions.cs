﻿using Common.Logging;
using Rhino.Files.Model;
using System;
using System.Collections.Generic;

namespace Rhino.Files.Storage
{
    internal class SenderActions : IDisposable
    {
        readonly QueueManagerConfiguration _configuration;
        readonly ILog _logger;

        public SenderActions(string database, QueueManagerConfiguration configuration)
        {
            _logger = LogManager.GetLogger(typeof(GlobalActions));
            _configuration = configuration;
        }

        public void Dispose()
        {
        }

        public IList<PersistentMessage> GetMessagesToSendAndMarkThemAsInFlight(int p1, int p2, out string point)
        {
            point = null;
            return null;
        }

        public void Commit()
        {
        }

        public void RevertBackToSend(MessageBookmark[] bookmarksToRevert)
        {
        }

        public void MarkOutgoingMessageAsFailedTransmission(MessageBookmark messageBookmark, bool p)
        {
        }

        public MessageBookmark MarkOutgoingMessageAsSuccessfullySent(MessageBookmark messageBookmark)
        {
            return null;
        }

        public IEnumerable<PersistentMessageToSend> GetMessagesToSend()
        {
            return null;
        }

        public bool HasMessagesToSend()
        {
            return false;
        }
    }
}

