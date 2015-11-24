using Common.Logging;
using Rhino.Files.Exceptions;
using Rhino.Files.Model;
using Rhino.FileWatch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Wintellect.Threading.AsyncProgModel;

namespace Rhino.Files.Protocol
{
    public class Receiver : IDisposable
    {
        readonly string _endpointToListenTo;
        readonly Func<Message[], IMessageAcceptance> _acceptMessages;
        FileWatcher _listener;
        readonly ILog _logger = LogManager.GetLogger(typeof(Receiver));

        public event Action CompletedRecievingMessages;

        public Receiver(string endpointToListenTo, Func<Message[], IMessageAcceptance> acceptMessages)
        {
            _endpointToListenTo = (!Path.IsPathRooted(endpointToListenTo) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, endpointToListenTo) : endpointToListenTo);
            _acceptMessages = acceptMessages;
        }

        public void Start()
        {
            _logger.DebugFormat("Starting to listen on {0}", _endpointToListenTo);
            _listener = new SimpleFileWatcher(_endpointToListenTo);
            _listener.Start();
            _listener.BeginAcceptFile(BeginAcceptFileCallback, null);
            _logger.DebugFormat("Now listen on {0}", _endpointToListenTo);
        }

        private void BeginAcceptFileCallback(IAsyncResult result)
        {
            FileWatcherFile file;
            try { file = _listener.EndAcceptFile(result); }
            catch (ObjectDisposedException) { return; }
            catch (Exception e) { _logger.Warn("Error on EndAcceptFile", e); StartAcceptingFile(); return; }

            _logger.DebugFormat("Accepting connection from {0}", file.EndPoint);
            var enumerator = new AsyncEnumerator("Receiver from " + file.EndPoint);
            enumerator.BeginExecute(ProcessRequest(file, enumerator), ar =>
            {
                try { enumerator.EndExecute(ar); }
                catch (Exception e) { _logger.Warn("Failed to recieve message", e); }
            });

            StartAcceptingFile();
        }

        private void StartAcceptingFile()
        {
            try { _listener.BeginAcceptFile(BeginAcceptFileCallback, null); }
            catch (ObjectDisposedException) { }
        }

        private IEnumerator<int> ProcessRequest(FileWatcherFile file, AsyncEnumerator ae)
        {
            //var destination = ""; // (!Path.IsPathRooted(Destination) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Destination) : Destination);
            var sender = file.EndPoint;
            try
            {
                var messages = new List<Message>();
                try
                {
                    foreach (var path in file.Paths)
                    {
                        byte[] buffer;
                        try { buffer = File.ReadAllBytes(path); }
                        catch (Exception e) { _logger.Warn("Unable to read length data from " + sender, e); yield break; }
                        var queue = Path.GetDirectoryName(path).Substring(sender.Length);
                        messages.Add(SerializationExtensions.ToMessage(buffer, queue, Path.GetFileName(path), File.GetCreationTime(path)));
                        File.Delete(path);
                    }
                    _logger.DebugFormat("Deserialized {0} messages from {1}", messages.Count, sender);
                }
                catch (Exception e) { messages = null; _logger.Warn("Failed to deserialize messages from " + sender, e); }

                //using (var stream = file.GetStream())
                //{
                //    var lenEnumerator = new AsyncEnumerator(ae.ToString());
                //    try { lenEnumerator.BeginExecute(StreamUtil.ReadBytes(lenOfDataToReadBuffer, stream, lenEnumerator, "length data", false), ae.End()); }
                //    catch (Exception exception) { _logger.Warn("Unable to read length data from " + remoteEndpoint, exception); yield break; }
                //    yield return 1;
                //    try { lenEnumerator.EndExecute(ae.DequeueAsyncResult()); }
                //    catch (Exception e) { _logger.Warn("Unable to read length data from " + remoteEndpoint, e); yield break; }

                //    var lengthOfDataToRead = BitConverter.ToInt32(lenOfDataToReadBuffer, 0);
                //    if (lengthOfDataToRead < 0) { _logger.WarnFormat("Got invalid length {0} from sender {1}", lengthOfDataToRead, remoteEndpoint); yield break; }
                //    _logger.DebugFormat("Reading {0} bytes from {1}", lengthOfDataToRead, remoteEndpoint);

                //    var buffer = new byte[lengthOfDataToRead];

                //    var readBufferEnumerator = new AsyncEnumerator(ae.ToString());
                //    try { readBufferEnumerator.BeginExecute(StreamUtil.ReadBytes(buffer, stream, readBufferEnumerator, "message data", false), ae.End()); }
                //    catch (Exception e) { _logger.Warn("Unable to read message data from " + remoteEndpoint, e); yield break; }
                //    yield return 1;
                //    try { readBufferEnumerator.EndExecute(ae.DequeueAsyncResult()); }
                //    catch (Exception e) { _logger.Warn("Unable to read message data from " + remoteEndpoint, e); yield break; }

                //    try { messages = SerializationExtensions.ToMessages(buffer); _logger.DebugFormat("Deserialized {0} messages from {1}", messages.Length, remoteEndpoint); }
                //    catch (Exception exception) { _logger.Warn("Failed to deserialize messages from " + remoteEndpoint, exception); }
                //}

                if (messages == null)
                {
                    //try { stream.BeginWrite(ProtocolConstants.SerializationFailureBuffer, 0, ProtocolConstants.SerializationFailureBuffer.Length, ae.End(), null); }
                    //catch (Exception e) { _logger.Warn("Unable to send serialization format error to " + sender, e); yield break; }
                    //yield return 1;
                    //try { stream.EndWrite(ae.DequeueAsyncResult()); }
                    //catch (Exception e) { _logger.Warn("Unable to send serialization format error to " + sender, e); }
                    yield break;
                }

                IMessageAcceptance acceptance = null;
                byte[] errorBytes = null;
                try { acceptance = _acceptMessages(messages.ToArray()); _logger.DebugFormat("All messages from {0} were accepted", sender); }
                catch (QueueDoesNotExistsException) { _logger.WarnFormat("Failed to accept messages from {0} because queue does not exists", sender); errorBytes = ProtocolConstants.QueueDoesNoExiststBuffer; }
                catch (Exception e) { errorBytes = ProtocolConstants.ProcessingFailureBuffer; _logger.Warn("Failed to accept messages from " + sender, e); }

                if (errorBytes != null)
                {
                    //try { stream.BeginWrite(errorBytes, 0, errorBytes.Length, ae.End(), null); }
                    //catch (Exception exception) { _logger.Warn("Unable to send processing failure from " + sender, exception); yield break; }
                    //yield return 1;
                    //try { stream.EndWrite(ae.DequeueAsyncResult()); }
                    //catch (Exception exception) { _logger.Warn("Unable to send processing failure from " + sender, exception); }
                    yield break;
                }

                //_logger.DebugFormat("Sending reciept notice to {0}", sender);
                //try { stream.BeginWrite(ProtocolConstants.RecievedBuffer, 0, ProtocolConstants.RecievedBuffer.Length, ae.End(), null); }
                //catch (Exception exception) { _logger.Warn("Could not send reciept notice to " + sender, exception); acceptance.Abort(); yield break; }
                //yield return 1;

                //try { stream.EndWrite(ae.DequeueAsyncResult()); }
                //catch (Exception exception) { _logger.Warn("Could not send reciept notice to " + sender, exception); acceptance.Abort(); yield break; }

                //_logger.DebugFormat("Reading acknowledgement about accepting messages to {0}", sender);

                //var acknowledgementBuffer = new byte[ProtocolConstants.AcknowledgedBuffer.Length];

                //var readAcknoweldgement = new AsyncEnumerator(ae.ToString());
                //try
                //{
                //    readAcknoweldgement.BeginExecute(
                //        StreamUtil.ReadBytes(acknowledgementBuffer, stream, readAcknoweldgement, "acknowledgement", false),
                //        ae.End());
                //}
                //catch (Exception exception)
                //{
                //    _logger.Warn("Error reading acknowledgement from " + sender, exception);
                //    acceptance.Abort();
                //    yield break;
                //}
                //yield return 1;
                //try
                //{
                //    readAcknoweldgement.EndExecute(ae.DequeueAsyncResult());
                //}
                //catch (Exception exception)
                //{
                //    _logger.Warn("Error reading acknowledgement from " + sender, exception);
                //    acceptance.Abort();
                //    yield break;
                //}

                //var senderResponse = Encoding.Unicode.GetString(acknowledgementBuffer);
                //if (senderResponse != ProtocolConstants.Acknowledged)
                //{
                //    _logger.WarnFormat("Sender did not respond with proper acknowledgement, the reply was {0}", senderResponse);
                //    acceptance.Abort();
                //}

                //bool commitSuccessful;
                try
                {
                    acceptance.Commit();
                    //commitSuccessful = true;
                }
                catch (Exception e)
                {
                    _logger.Warn("Unable to commit messages from " + sender, e);
                    //commitSuccessful = false;
                }

                //if (!commitSuccessful)
                //{
                //    bool writeSuccessful;
                //    try { stream.BeginWrite(ProtocolConstants.RevertBuffer, 0, ProtocolConstants.RevertBuffer.Length, ae.End(), null); writeSuccessful = true; }
                //    catch (Exception e) { _logger.Warn("Unable to send revert message to " + sender, e); writeSuccessful = false; }

                //    if (writeSuccessful)
                //    {
                //        yield return 1;
                //        try { stream.EndWrite(ae.DequeueAsyncResult()); }
                //        catch (Exception exception) { _logger.Warn("Unable to send revert message to " + sender, exception); }
                //    }
                //}
            }
            finally
            {
                var copy = CompletedRecievingMessages;
                if (copy != null)
                    copy();
            }
        }

        public void Dispose()
        {
            _listener.Stop();
        }
    }
}