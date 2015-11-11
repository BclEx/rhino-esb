using Common.Logging;
using Rhino.Files.Model;
using Rhino.Files.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wintellect.Threading.AsyncProgModel;

namespace Rhino.Files.Protocol
{
    public class Sender : IDisposable
    {
        readonly ILog _logger = LogManager.GetLogger(typeof(Sender));

        public event Action SendCompleted;
        public Func<MessageBookmark[]> Success { get; set; }
        public Action<Exception> Failure { get; set; }
        public Action<MessageBookmark[]> Revert { get; set; }
        public string Destination { get; set; }
        public Message[] Messages { get; set; }
        public Action Commit { get; set; }

        public Sender()
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
            var destination = (!Path.IsPathRooted(Destination) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Destination) : Destination);
            try
            {
                try { if (!Directory.Exists(destination)) Directory.CreateDirectory(destination); }
                catch (Exception e) { _logger.WarnFormat("Failed to connect to {0} because {1}", Destination, e); Failure(e); yield break; }
                yield return 1;
                _logger.DebugFormat("Successfully connected to {0}", Destination);

                var filename = Path.Combine(destination, Guid.NewGuid().ToString());
                var buffer = Messages.Serialize();
                var bufferLenInBytes = BitConverter.GetBytes(buffer.Length);
                _logger.DebugFormat("Writing length of {0} bytes to {1}", buffer.Length, Destination);

                using (var stream = File.Create(filename))
                {
                    try { stream.BeginWrite(bufferLenInBytes, 0, bufferLenInBytes.Length, ae.End(), null); }
                    catch (Exception e) { _logger.WarnFormat("Could not write to {0} because {1}", Destination, e); Failure(e); yield break; }
                    yield return 1;

                    try { stream.EndWrite(ae.DequeueAsyncResult()); }
                    catch (Exception e) { _logger.WarnFormat("Could not write to {0} because {1}", Destination, e); Failure(e); yield break; }
                    _logger.DebugFormat("Writing {0} bytes to {1}", buffer.Length, Destination);

                    try { stream.BeginWrite(buffer, 0, buffer.Length, ae.End(), null); }
                    catch (Exception e) { _logger.WarnFormat("Could not write to {0} because {1}", Destination, e); Failure(e); yield break; }
                    yield return 1;

                    try { stream.EndWrite(ae.DequeueAsyncResult()); }
                    catch (Exception e) { _logger.WarnFormat("Could not write to {0} because {1}", Destination, e); Failure(e); yield break; }
                    _logger.DebugFormat("Successfully wrote to {0}", Destination);
                }

                //var recieveBuffer = new byte[ProtocolConstants.RecievedBuffer.Length];
                //var readConfirmationEnumerator = new AsyncEnumerator();
                //try { readConfirmationEnumerator.BeginExecute(StreamUtil.ReadBytes(recieveBuffer, stream, readConfirmationEnumerator, "recieve confirmation", false), ae.End()); }
                //catch (Exception e) { _logger.WarnFormat("Could not read confirmation from {0} because {1}", Destination, e); Failure(e); yield break; }
                //yield return 1;

                //try { readConfirmationEnumerator.EndExecute(ae.DequeueAsyncResult()); }
                //catch (Exception e) { _logger.WarnFormat("Could not read confirmation from {0} because {1}", Destination, e); Failure(e); yield break; }

                //var recieveRespone = Encoding.Unicode.GetString(recieveBuffer);
                //if (recieveRespone == ProtocolConstants.QueueDoesNotExists) { _logger.WarnFormat("Response from reciever {0} is that queue does not exists", Destination); Failure(new QueueDoesNotExistsException()); yield break; }
                //else if (recieveRespone != ProtocolConstants.Recieved) { _logger.WarnFormat("Response from reciever {0} is not the expected one, unexpected response was: {1}", Destination, recieveRespone); Failure(null); yield break; }

                //try { stream.BeginWrite(ProtocolConstants.AcknowledgedBuffer, 0, ProtocolConstants.AcknowledgedBuffer.Length, ae.End(), null); }
                //catch (Exception e) { _logger.WarnFormat("Failed to write acknowledgement to reciever {0} because {1}", Destination, e); Failure(e); yield break; }
                //yield return 1;

                //try { stream.EndWrite(ae.DequeueAsyncResult()); }
                //catch (Exception e) { _logger.WarnFormat("Failed to write acknowledgement to reciever {0} because {1}", Destination, e); Failure(e); yield break; }

                //var bookmarks = Success();

                //buffer = new byte[ProtocolConstants.RevertBuffer.Length];
                //var readRevertMessage = new AsyncEnumerator(ae.ToString());
                //var startingToReadFailed = false;
                //try { readRevertMessage.BeginExecute(StreamUtil.ReadBytes(buffer, stream, readRevertMessage, "revert", true), ae.End()); }
                //catch (Exception) { startingToReadFailed = true; } // more or less expected
                //if (startingToReadFailed)
                //    yield break;
                //yield return 1;
                //try
                //{
                //    readRevertMessage.EndExecute(ae.DequeueAsyncResult());
                //    var revert = Encoding.Unicode.GetString(buffer);
                //    if (revert == ProtocolConstants.Revert)
                //    {
                //        _logger.Warn("Got back revert message from receiver, reverting send");
                //        Revert(bookmarks);
                //    }
                //}
                //catch (Exception)
                //{
                //    // expected, there is nothing to do here, the
                //    // reciever didn't report anything for us
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