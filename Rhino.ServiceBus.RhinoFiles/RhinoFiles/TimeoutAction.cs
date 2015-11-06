using Common.Logging;
using Rhino.Files;
using Rhino.Files.Model;
using Rhino.ServiceBus.DataStructures;
using Rhino.ServiceBus.Transport;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Transactions;
using System.Xml;
using Message = Rhino.Files.Model.Message;

namespace Rhino.ServiceBus.RhinoFiles
{
    public class TimeoutAction : IDisposable
    {
        readonly IQueue _queue;
        readonly ILog _logger = LogManager.GetLogger(typeof(TimeoutAction));
        readonly Timer _timeoutTimer;
        readonly OrderedList<DateTime, MessageId> _timeoutMessageIds = new OrderedList<DateTime, MessageId>();

        [CLSCompliant(false)]
        public TimeoutAction(IQueue queue)
        {
            _queue = queue;
            _timeoutMessageIds.Write(writer =>
            {
                foreach (var message in queue.GetAllMessages(SubQueue.Timeout.ToString()))
                {
                    var timeToSend = XmlConvert.ToDateTime(message.Headers["time-to-send"], XmlDateTimeSerializationMode.Utc);
                    _logger.DebugFormat("Registering message {0} to be sent at {1} on {2}", message.Id, timeToSend, queue.QueueName);
                    writer.Add(timeToSend, message.Id);
                }
            });
            _timeoutTimer = new Timer(OnTimeoutCallback, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
        }

        public static DateTime CurrentTime
        {
            get { return DateTime.Now; }
        }

        private void OnTimeoutCallback(object state)
        {
            var haveTimeoutMessages = false;

            _timeoutMessageIds.Read(reader => haveTimeoutMessages = reader.HasAnyBefore(CurrentTime));

            if (!haveTimeoutMessages)
                return;

            _timeoutMessageIds.Write(writer =>
            {
                KeyValuePair<DateTime, List<MessageId>> pair;
                while (writer.TryRemoveFirstUntil(CurrentTime, out pair))
                {
                    if (pair.Key > CurrentTime)
                        return;
                    foreach (var messageId in pair.Value)
                    {
                        try
                        {
                            _logger.DebugFormat("Moving message {0} to main queue: {1}", messageId, _queue.QueueName);
                            using (var tx = new TransactionScope())
                            {
                                var message = _queue.PeekById(messageId);
                                if (message == null)
                                    continue;
                                _queue.MoveTo(null, message);
                                tx.Complete();
                            }
                        }
                        catch (Exception)
                        {
                            _logger.DebugFormat("Could not move message {0} to main queue: {1}", messageId, _queue.QueueName);

                            if ((CurrentTime - pair.Key).TotalMinutes >= 1.0D)
                            {
                                _logger.DebugFormat("Tried to send message {0} for over a minute, giving up", messageId);
                                continue;
                            }

                            writer.Add(pair.Key, messageId);
                            _logger.DebugFormat("Will retry moving message {0} to main queue {1} in 1 second", messageId, _queue.QueueName);
                        }
                    }
                }
            });
        }

        public void Dispose()
        {
            if (_timeoutTimer != null)
                _timeoutTimer.Dispose();
        }

        [CLSCompliant(false)]
        public void Register(Message message)
        {
            _timeoutMessageIds.Write(writer =>
            {
                var timeToSend = XmlConvert.ToDateTime(message.Headers["time-to-send"], XmlDateTimeSerializationMode.Utc);
                _logger.DebugFormat("Registering message {0} to be sent at {1} on {2}", message.Id, timeToSend, _queue.QueueName);
                writer.Add(timeToSend, message.Id);
            });
        }
    }
}