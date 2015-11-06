using Rhino.Files;
using Rhino.ServiceBus.DataStructures;
using Rhino.ServiceBus.Impl;
using Rhino.ServiceBus.Internal;
using Rhino.ServiceBus.Transport;
using System;
using System.Text;
using System.Transactions;

namespace Rhino.ServiceBus.RhinoFiles
{
    public class ErrorAction
    {
        readonly int _numberOfRetries;
        readonly Hashtable<string, ErrorCounter> _failureCounts = new Hashtable<string, ErrorCounter>();

        public ErrorAction(int numberOfRetries)
        {
            _numberOfRetries = numberOfRetries;
        }

        public void Init(ITransport transport)
        {
            transport.MessageSerializationException += Transport_OnMessageSerializationException;
            transport.MessageProcessingFailure += Transport_OnMessageProcessingFailure;
            transport.MessageProcessingCompleted += Transport_OnMessageProcessingCompleted;
            transport.MessageArrived += Transport_OnMessageArrived;
        }

        private bool Transport_OnMessageArrived(CurrentMessageInformation information)
        {
            var info = (RhinoFileCurrentMessageInformation)information;
            ErrorCounter val = null;
            _failureCounts.Read(reader => reader.TryGetValue(info.TransportMessageId, out val));
            if (val == null || val.FailureCount < _numberOfRetries)
                return false;

            var result = false;
            _failureCounts.Write(writer =>
            {
                if (!writer.TryGetValue(info.TransportMessageId, out val))
                    return;

                info.Queue.MoveTo(SubQueue.Errors.ToString(), info.TransportMessage);
                info.Queue.EnqueueDirectlyTo(SubQueue.Errors.ToString(), new MessagePayload
                {
                    Data = (val.ExceptionText == null ? null : Encoding.Unicode.GetBytes(val.ExceptionText)),
                    Headers =
                    {
                        {"correlation-id", info.TransportMessageId},
                        {"retries", val.FailureCount.ToString()}
                    }
                });

                result = true;
            });

            return result;
        }

        private void Transport_OnMessageSerializationException(CurrentMessageInformation information, Exception e)
        {
            var info = (RhinoFileCurrentMessageInformation)information;
            _failureCounts.Write(writer => writer.Add(info.TransportMessageId, new ErrorCounter
            {
                ExceptionText = (e == null ? null : e.ToString()),
                FailureCount = _numberOfRetries + 1
            }));

            using (var tx = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                info.Queue.MoveTo(SubQueue.Errors.ToString(), info.TransportMessage);
                info.Queue.EnqueueDirectlyTo(SubQueue.Errors.ToString(), new MessagePayload
                {
                    Data = (e == null ? null : Encoding.Unicode.GetBytes(e.ToString())),
                    Headers =
					{
						{"correlation-id", info.TransportMessageId},
						{"retries", "1"}
					}
                });
                tx.Complete();
            }
        }

        private void Transport_OnMessageProcessingCompleted(CurrentMessageInformation information, Exception e)
        {
            if (e != null)
                return;

            ErrorCounter val = null;
            _failureCounts.Read(reader => reader.TryGetValue(information.TransportMessageId, out val));
            if (val == null)
                return;
            _failureCounts.Write(writer => writer.Remove(information.TransportMessageId));
        }

        private void Transport_OnMessageProcessingFailure(CurrentMessageInformation information, Exception e)
        {
            _failureCounts.Write(writer =>
            {
                ErrorCounter errorCounter;
                if (writer.TryGetValue(information.TransportMessageId, out errorCounter) == false)
                {
                    errorCounter = new ErrorCounter
                    {
                        ExceptionText = (e == null ? null : e.ToString()),
                        FailureCount = 0
                    };
                    writer.Add(information.TransportMessageId, errorCounter);
                }
                errorCounter.FailureCount += 1;
            });
        }

        private class ErrorCounter
        {
            public string ExceptionText;
            public int FailureCount;
        }
    }
}