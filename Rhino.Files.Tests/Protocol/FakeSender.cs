using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Common.Logging;
using Rhino.Files.Exceptions;
using Rhino.Files.Model;
using Rhino.Files.Protocol;
using Rhino.Files.Storage;
using Wintellect.Threading.AsyncProgModel;

namespace Rhino.Files.Tests.Protocol
{
    public class FakeSender : IDisposable
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(FakeSender));

        public bool FailToAcknowledgeReceipt;

        public event Action SendCompleted;
        public Func<MessageBookmark[]> Success { get; set; }
        public Action<Exception> Failure { get; set; }
        public Action<MessageBookmark[]> Revert { get; set; }
        public Action Commit { get; set; }
        public string Destination { get; set; }
        public Message[] Messages { get; set; }

        public FakeSender()
        {
            Failure = e => { };
            Success = () => null;
            Revert = bookmarks => { };
            Commit = () => { };
        }

        public void Send()
        {
            var enumerator = new AsyncEnumerator(string.Format("Sending {0} messages to {1}", Messages.Length, Destination));
            _logger.DebugFormat("Starting to send {0} messages to {1}", Messages.Length, Destination);
            enumerator.BeginExecute(SendInternal(enumerator), result =>
            {
                try { enumerator.EndExecute(result); }
                catch (Exception e) { _logger.Warn("Failed to send message", e); Failure(e); }
            });
        }

        private IEnumerator<int> SendInternal(AsyncEnumerator ae)
        {
            try
            {
                return null;
                //using (var client = new TcpClient())
                //{
                //    try
                //    {
                //        client.BeginConnect(Destination, Destination.Port,
                //                            ae.End(),
                //                            null);
                //    }
                //    catch (Exception e)
                //    {
                //        _logger.WarnFormat("Failed to connect to {0} because {1}", Destination, e);
                //        Failure(e);
                //        yield break;
                //    }

                //    yield return 1;

                //    try
                //    {
                //        client.EndConnect(ae.DequeueAsyncResult());
                //    }
                //    catch (Exception e)
                //    {
                //        _logger.WarnFormat("Failed to connect to {0} because {1}", Destination, e);
                //        Failure(e);
                //        yield break;
                //    }

                //    _logger.DebugFormat("Successfully connected to {0}", Destination);

                //    using (var stream = client.GetStream())
                //    {
                //        var buffer = Messages.Serialize();

                //        var bufferLenInBytes = BitConverter.GetBytes(buffer.Length);

                //        _logger.DebugFormat("Writing length of {0} bytes to {1}", buffer.Length, Destination);

                //        try
                //        {
                //            stream.BeginWrite(bufferLenInBytes, 0, bufferLenInBytes.Length, ae.End(), null);
                //        }
                //        catch (Exception e)
                //        {
                //            _logger.WarnFormat("Could not write to {0} because {1}", Destination,
                //                              e);
                //            Failure(e);
                //            yield break;
                //        }

                //        yield return 1;

                //        try
                //        {
                //            stream.EndWrite(ae.DequeueAsyncResult());
                //        }
                //        catch (Exception e)
                //        {
                //            _logger.WarnFormat("Could not write to {0} because {1}", Destination,
                //                              e);
                //            Failure(e);
                //            yield break;
                //        }

                //        _logger.DebugFormat("Writing {0} bytes to {1}", buffer.Length, Destination);

                //        try
                //        {
                //            stream.BeginWrite(buffer, 0, buffer.Length, ae.End(), null);
                //        }
                //        catch (Exception e)
                //        {
                //            _logger.WarnFormat("Could not write to {0} because {1}", Destination,
                //                            e);
                //            Failure(e);
                //            yield break;
                //        }

                //        yield return 1;

                //        try
                //        {
                //            stream.EndWrite(ae.DequeueAsyncResult());
                //        }
                //        catch (Exception e)
                //        {
                //            _logger.WarnFormat("Could not write to {0} because {1}", Destination,
                //                              e);
                //            Failure(e);
                //            yield break;
                //        }

                //        _logger.DebugFormat("Successfully wrote to {0}", Destination);

                //        var recieveBuffer = new byte[ProtocolConstants.RecievedBuffer.Length];
                //        var readConfirmationEnumerator = new AsyncEnumerator();

                //        try
                //        {
                //            readConfirmationEnumerator.BeginExecute(
                //                StreamUtil.ReadBytes(recieveBuffer, stream, readConfirmationEnumerator, "recieve confirmation", false), ae.End());
                //        }
                //        catch (Exception exception)
                //        {
                //            _logger.WarnFormat("Could not read confirmation from {0} because {1}", Destination,
                //                              exception);
                //            Failure(exception);
                //            yield break;
                //        }

                //        yield return 1;

                //        try
                //        {
                //            readConfirmationEnumerator.EndExecute(ae.DequeueAsyncResult());
                //        }
                //        catch (Exception exception)
                //        {
                //            _logger.WarnFormat("Could not read confirmation from {0} because {1}", Destination,
                //                              exception);
                //            Failure(exception);
                //            yield break;
                //        }

                //        var recieveRespone = Encoding.Unicode.GetString(recieveBuffer);
                //        if (recieveRespone == ProtocolConstants.QueueDoesNotExists)
                //        {
                //            _logger.WarnFormat(
                //                "Response from reciever {0} is that queue does not exists",
                //                Destination);
                //            Failure(new QueueDoesNotExistsException());
                //            yield break;
                //        }
                //        else if (recieveRespone != ProtocolConstants.Recieved)
                //        {
                //            _logger.WarnFormat(
                //                "Response from reciever {0} is not the expected one, unexpected response was: {1}",
                //                Destination, recieveRespone);
                //            Failure(null);
                //            yield break;
                //        }

                //        try
                //        {
                //            if (FailToAcknowledgeReceipt)
                //                yield break;

                //            stream.BeginWrite(ProtocolConstants.AcknowledgedBuffer, 0,
                //                              ProtocolConstants.AcknowledgedBuffer.Length, ae.End(), null);
                //        }
                //        catch (Exception e)
                //        {
                //            _logger.WarnFormat("Failed to write acknowledgement to reciever {0} because {1}",
                //                              Destination, e);
                //            Failure(e);
                //            yield break;
                //        }

                //        yield return 1;

                //        try
                //        {
                //            stream.EndWrite(ae.DequeueAsyncResult());
                //        }
                //        catch (Exception e)
                //        {
                //            _logger.WarnFormat("Failed to write acknowledgement to reciever {0} because {1}",
                //                              Destination, e);
                //            Failure(e);
                //            yield break;
                //        }

                //        var bookmarks = Success();

                //        buffer = new byte[ProtocolConstants.RevertBuffer.Length];
                //        var readRevertMessage = new AsyncEnumerator(ae.ToString());
                //        bool startingToReadFailed = false;
                //        try
                //        {
                //            readRevertMessage.BeginExecute(
                //                StreamUtil.ReadBytes(buffer, stream, readRevertMessage, "revert", true), ae.End());
                //        }
                //        catch (Exception)
                //        {
                //            //more or less expected
                //            startingToReadFailed = true;
                //        }
                //        if (startingToReadFailed)
                //        {
                //            Commit();
                //            yield break;
                //        }
                //        yield return 1;
                //        try
                //        {
                //            readRevertMessage.EndExecute(ae.DequeueAsyncResult());
                //            var revert = Encoding.Unicode.GetString(buffer);
                //            if (revert == ProtocolConstants.Revert)
                //            {
                //                _logger.Warn("Got back revert message from receiver, reverting send");
                //                Revert(bookmarks);
                //            }
                //            else
                //                Commit();
                //        }
                //        catch (Exception)
                //        {
                //            // expected, there is nothing to do here, the
                //            // reciever didn't report anything for us
                //            Commit();
                //        }

                //    }
                //}
            }
            finally
            {
                var completed = SendCompleted;
                if (completed != null)
                    completed();
            }
        }

        public void Dispose()
        {
        }
    }
}
