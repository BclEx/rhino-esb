using Common.Logging;
using Rhino.Files.Model;
using Rhino.Files.Storage;
using System;
using System.Collections.Generic;
using System.IO;
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
        public Action Connected { get; set; }
        public Action<Exception> FailureToConnect { get; set; }
        public Action Commit { get; set; }
        public string Destination { get; set; }
        public Message[] Messages { get; set; }

        public Sender()
        {
            Connected = () => { };
            FailureToConnect = e => { };
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
                //try { if (!Directory.Exists(destination)) throw new InvalidOperationException(string.Format("Destination {0} does not exist", destination)); }
                try { if (!Directory.Exists(destination)) Directory.CreateDirectory(destination); }
                catch (Exception e) { _logger.WarnFormat("Failed to connect to {0} because {1}", Destination, e); FailureToConnect(e); yield break; }

                try { Connected(); }
                catch (Exception e) { _logger.WarnFormat("Failed to connect to {0} because {1}", Destination, e); FailureToConnect(e); yield break; }
                _logger.DebugFormat("Successfully connected to {0}", Destination);

                var moves = new List<string>();
                foreach (var message in Messages)
                {
                    string queue; string file; DateTime fileDate;
                    var buffer = message.Serialize(out queue, out file, out fileDate);
                    _logger.DebugFormat("Writing length of {0} bytes to {1}", buffer.Length, Destination);

                    if (!Directory.Exists(Path.Combine(destination, queue))) Directory.CreateDirectory(Path.Combine(destination, queue));
                    //if (!Directory.Exists(Path.Combine(destination, queue.Split('\\')[0]))) { _logger.WarnFormat("Response from reciever {0} is that queue does not exists", Destination); Failure(new QueueDoesNotExistsException()); yield break; }

                    var filename = Path.Combine(destination, queue, file);
                    var filename2 = filename + ".sending";
                    using (var s = File.Create(filename2))
                    {
                        try { s.BeginWrite(buffer, 0, buffer.Length, ae.End(), null); }
                        catch (Exception e) { _logger.WarnFormat("Could not write to {0} because {1}", Destination, e); Failure(e); yield break; }
                        yield return 1;
                        try { s.EndWrite(ae.DequeueAsyncResult()); }
                        catch (Exception e) { _logger.WarnFormat("Could not write to {0} because {1}", Destination, e); Failure(e); yield break; }
                        _logger.DebugFormat("Writing {0} bytes to {1}", buffer.Length, Destination);
                    }
                    try { File.SetCreationTime(filename2, fileDate); }
                    catch (Exception e) { _logger.WarnFormat("Could not set creation time to {0} because {1}", Destination, e); Failure(e); yield break; }
                    moves.Add(filename);
                }
                _logger.DebugFormat("Successfully wrote to {0}", Destination);

                string lastMove = null;
                try { foreach (var move in moves) { lastMove = move + ".sending"; File.Move(lastMove, move); } }
                catch (Exception e) { _logger.WarnFormat("Could move file {0} because {1}", Destination, e); Failure(e); yield break; }

                var bookmarks = Success();
                //Revert(bookmarks);
                Commit();
                yield break;
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