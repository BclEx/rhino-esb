using Common.Logging;
using Rhino.Files;
using Rhino.Files.Model;
using Rhino.HashTables;
using Rhino.ServiceBus.Impl;
using Rhino.ServiceBus.Internal;
using Rhino.ServiceBus.Transport;
using Rhino.ServiceBus.Util;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Transactions;
using System.Xml;
using Transaction = System.Transactions.Transaction;

namespace Rhino.ServiceBus.RhinoFiles
{
    [CLSCompliant(false)]
    public class RhinoFilesTransport : ITransport
    {
        Uri _endpoint;
        readonly IEndpointRouter _endpointRouter;
        readonly IMessageSerializer _messageSerializer;
        readonly int _threadCount;
        readonly string _path;
        QueueManager _queueManager;
        readonly Thread[] _threads;
        readonly string _queueName;
        volatile int _currentlyProccessingCount;
        volatile bool _shouldContinue;
        bool _haveStarted;
        readonly IsolationLevel _queueIsolationLevel;
        readonly int _numberOfRetries;
        readonly bool _enablePerformanceCounters;
        readonly IMessageBuilder<MessagePayload> _messageBuilder;
        readonly QueueManagerConfiguration _queueManagerConfiguration;

        [ThreadStatic]
        static RhinoFileCurrentMessageInformation _currentMessageInformation;

        readonly ILog _logger = LogManager.GetLogger(typeof(RhinoFilesTransport));
        TimeoutAction _timeout;
        IQueue _queue;

        public RhinoFilesTransport(Uri endpoint,
            IEndpointRouter endpointRouter,
            IMessageSerializer messageSerializer,
            int threadCount,
            string path,
            IsolationLevel queueIsolationLevel,
            int numberOfRetries,
            bool enablePerformanceCounters,
            IMessageBuilder<MessagePayload> messageBuilder,
            QueueManagerConfiguration queueManagerConfiguration)
        {
            _endpoint = endpoint;
            _queueIsolationLevel = queueIsolationLevel;
            _numberOfRetries = numberOfRetries;
            _enablePerformanceCounters = enablePerformanceCounters;
            _messageBuilder = messageBuilder;
            _queueManagerConfiguration = queueManagerConfiguration;
            _endpointRouter = endpointRouter;
            _messageSerializer = messageSerializer;
            _threadCount = threadCount;
            _path = path;

            _queueName = endpoint.GetQueueName();

            _threads = new Thread[threadCount];

            // This has to be the first subscriber to the transport events in order to successfully handle the errors semantics
            new ErrorAction(numberOfRetries).Init(this);
            messageBuilder.Initialize(Endpoint);
        }

        public void Dispose()
        {
            _shouldContinue = false;
            _logger.DebugFormat("Stopping transport for {0}", _endpoint);

            while (_currentlyProccessingCount > 0)
                Thread.Sleep(TimeSpan.FromSeconds(1));

            if (_timeout != null)
                _timeout.Dispose();
            DisposeQueueManager();

            if (!_haveStarted)
                return;
            foreach (var thread in _threads)
                thread.Join();
        }

        private void DisposeQueueManager()
        {
            if (_queueManager != null)
            {
                const int retries = 5;
                var tries = 0;
                var disposeRudely = false;
                while (true)
                {
                    try { _queueManager.Dispose(); break; }
                    catch (HashTableException e)
                    {
                        tries += 1;
                        if (tries > retries)
                        {
                            disposeRudely = true;
                            break;
                        }
                        if (e.Error != HashTableError.TooManyActiveUsers)
                            throw;
                        // let the other threads a chance to complete their work
                        Thread.Sleep(50);
                    }
                }
                if (disposeRudely)
                    _queueManager.DisposeRudely();
            }
        }

        [CLSCompliant(false)]
        public IQueue Queue
        {
            get { return _queue; }
        }

        public void Start()
        {
            if (_haveStarted)
                return;

            _shouldContinue = true;

            ConfigureAndStartQueueManager(_endpoint.Host);

            _queue = _queueManager.GetQueue(_queueName);

            _timeout = new TimeoutAction(_queue);
            _logger.DebugFormat("Starting {0} threads to handle messages on {1}, number of retries: {2}", _threadCount, _endpoint, _numberOfRetries);
            for (var i = 0; i < _threadCount; i++)
            {
                _threads[i] = new Thread(ReceiveMessage)
                {
                    Name = "Rhino Service Bus Worker Thread #" + i,
                    IsBackground = true
                };
                _threads[i].Start(i);
            }
            _haveStarted = true;
            var started = Started;
            if (started != null)
                started();
        }

        private void ConfigureAndStartQueueManager(string endpoint)
        {
            _queueManager = new QueueManager(endpoint, _path, _queueManagerConfiguration);
            _queueManager.CreateQueues(_queueName);

            if (_enablePerformanceCounters)
                _queueManager.EnablePerformanceCounters();

            _queueManager.Start();
        }

        private void ReceiveMessage(object context)
        {
            while (_shouldContinue)
            {
                try { _queueManager.Peek(_queueName); }
                catch (TimeoutException) { _logger.DebugFormat("Could not find a message on {0} during the timeout period", _endpoint); continue; }
                catch (ObjectDisposedException) { _logger.DebugFormat("Shutting down the transport for {0} thread {1}", _endpoint, context); return; }
                catch (HashTableException e)
                {
                    // we were shut down, so just return
                    if (e.Error == HashTableError.TermInProgress)
                        _logger.DebugFormat("Shutting down the transport for {0} thread {1}", _endpoint, context);
                    else
                        _logger.Error("An error occured while recieving a message, shutting down message processing thread", e);
                    return;
                }

                if (!_shouldContinue)
                    return;

                var transactionOptions = GetTransactionOptions();
                using (var tx = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
                {
                    Message message;
                    try { message = _queueManager.Receive(_queueName, TimeSpan.FromSeconds(1)); }
                    catch (TimeoutException) { _logger.DebugFormat("Could not find a message on {0} during the timeout period", _endpoint); continue; }
                    catch (HashTableException e)
                    {
                        // we were shut down, so just return
                        if (e.Error == HashTableError.TermInProgress)
                            return;
                        _logger.Error("An error occured while recieving a message, shutting down message processing thread", e);
                        return;
                    }
                    catch (Exception e) { _logger.Error("An error occured while recieving a message, shutting down message processing thread", e); return; }

                    try
                    {
                        var msgType = (MessageType)Enum.Parse(typeof(MessageType), message.Headers["type"]);
                        _logger.DebugFormat("Starting to handle message {0} of type {1} on {2}", message.Id, msgType, _endpoint);
                        switch (msgType)
                        {
                            case MessageType.AdministrativeMessageMarker:
                                ProcessMessage(message, tx, AdministrativeMessageArrived, AdministrativeMessageProcessingCompleted, null, null);
                                break;
                            case MessageType.ShutDownMessageMarker:
                                //ignoring this one
                                tx.Complete();
                                break;
                            case MessageType.TimeoutMessageMarker:
                                var timeToSend = XmlConvert.ToDateTime(message.Headers["time-to-send"], XmlDateTimeSerializationMode.Utc);
                                if (timeToSend > DateTime.Now)
                                {
                                    _timeout.Register(message);
                                    _queue.MoveTo(SubQueue.Timeout.ToString(), message);
                                    tx.Complete();
                                }
                                else
                                    ProcessMessage(message, tx, MessageArrived, MessageProcessingCompleted, BeforeMessageTransactionCommit, BeforeMessageTransactionRollback);
                                break;
                            default:
                                ProcessMessage(message, tx, MessageArrived, MessageProcessingCompleted, BeforeMessageTransactionCommit, BeforeMessageTransactionRollback);
                                break;
                        }
                    }
                    catch (Exception e) { _logger.Debug("Could not process message", e); }
                }
            }
        }

        private void ProcessMessage(Message message, TransactionScope tx, Func<CurrentMessageInformation, bool> messageRecieved, Action<CurrentMessageInformation, Exception> messageCompleted, Action<CurrentMessageInformation> beforeTransactionCommit, Action<CurrentMessageInformation> beforeTransactionRollback)
        {
            Exception ex = null;
            try
            {
                //deserialization errors do not count for module events
#pragma warning disable 420
                Interlocked.Increment(ref _currentlyProccessingCount);
                var messages = DeserializeMessages(message);
                try
                {
                    var messageId = new Guid(message.Headers["id"]);
                    var source = new Uri(message.Headers["source"]);
                    foreach (var msg in messages)
                    {
                        _currentMessageInformation = new RhinoFileCurrentMessageInformation
                        {
                            AllMessages = messages,
                            Message = msg,
                            Destination = _endpoint,
                            MessageId = messageId,
                            Source = source,
                            TransportMessageId = message.Id.ToString(),
                            Queue = _queue,
                            TransportMessage = message
                        };

                        if (!TransportUtil.ProcessSingleMessage(_currentMessageInformation, messageRecieved))
                            Discard(_currentMessageInformation.Message);
                    }
                }
                catch (Exception e) { ex = e; _logger.Error("Failed to process message", e); }
            }
            catch (Exception e) { ex = e; _logger.Error("Failed to deserialize message", e); }
            finally
            {
                var messageHandlingCompletion = new MessageHandlingCompletion(tx, null, ex, messageCompleted, beforeTransactionCommit, beforeTransactionRollback, _logger, MessageProcessingFailure, _currentMessageInformation);
                messageHandlingCompletion.HandleMessageCompletion();
                _currentMessageInformation = null;
                Interlocked.Decrement(ref _currentlyProccessingCount);
#pragma warning restore 420
            }
        }

        private void Discard(object message)
        {
            _logger.DebugFormat("Discarding message {0} ({1}) because there are no consumers for it.",
                message, _currentMessageInformation.TransportMessageId);
            Send(new Endpoint { Uri = _endpoint.AddSubQueue(SubQueue.Discarded) }, new[] { message });
        }

        private object[] DeserializeMessages(Message message)
        {
            try { return _messageSerializer.Deserialize(new MemoryStream(message.Data)); }
            catch (Exception e)
            {
                try
                {
                    _logger.Error("Error when serializing message", e);
                    var serializationError = MessageSerializationException;
                    if (serializationError != null)
                    {
                        _currentMessageInformation = new RhinoFileCurrentMessageInformation
                        {
                            Message = message,
                            Source = new Uri(message.Headers["source"]),
                            MessageId = new Guid(message.Headers["id"]),
                            TransportMessageId = message.Id.ToString(),
                            TransportMessage = message,
                            Queue = _queue,
                        };
                        serializationError(_currentMessageInformation, e);
                    }
                }
                catch (Exception e2) { _logger.Error("Error when notifying about serialization exception", e2); }
                throw;
            }
        }

        public Endpoint Endpoint
        {
            get { return _endpointRouter.GetRoutedEndpoint(_endpoint); }
        }

        public int ThreadCount
        {
            get { return _threadCount; }
        }

        public CurrentMessageInformation CurrentMessageInformation
        {
            get { return _currentMessageInformation; }
        }

        public void Send(Endpoint destination, object[] msgs)
        {
            SendInternal(msgs, destination, nv => { });
        }

        private void SendInternal(object[] msgs, Endpoint destination, Action<NameValueCollection> customizeHeaders)
        {
            var messageId = Guid.NewGuid();
            var messageInformation = new OutgoingMessageInformation
            {
                Destination = destination,
                Messages = msgs,
                Source = Endpoint
            };
            var payload = _messageBuilder.BuildFromMessageBatch(messageInformation);
            _logger.DebugFormat("Sending a message with id '{0}' to '{1}'", messageId, destination.Uri);
            customizeHeaders(payload.Headers);
            var transactionOptions = GetTransactionOptions();
            using (var tx = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                _queueManager.Send(destination.Uri, payload);
                tx.Complete();
            }

            var copy = MessageSent;
            if (copy == null)
                return;

            copy(new RhinoFileCurrentMessageInformation
            {
                AllMessages = msgs,
                Source = Endpoint.Uri,
                Destination = destination.Uri,
                MessageId = messageId,
            });
        }

        private TransactionOptions GetTransactionOptions()
        {
            return new TransactionOptions
            {
                IsolationLevel = (Transaction.Current == null ? _queueIsolationLevel : Transaction.Current.IsolationLevel),
                Timeout = TransportUtil.GetTransactionTimeout(),
            };
        }

        public void Send(Endpoint endpoint, DateTime processAgainAt, object[] msgs)
        {
            SendInternal(msgs, endpoint,
                nv =>
                {
                    nv["time-to-send"] = processAgainAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                    nv["type"] = MessageType.TimeoutMessageMarker.ToString();
                });
        }

        public void Reply(params object[] messages)
        {
            Send(new Endpoint { Uri = _currentMessageInformation.Source }, messages);
        }

        public event Action<CurrentMessageInformation> MessageSent;
        public event Func<CurrentMessageInformation, bool> AdministrativeMessageArrived;
        public event Func<CurrentMessageInformation, bool> MessageArrived;
        public event Action<CurrentMessageInformation, Exception> MessageSerializationException;
        public event Action<CurrentMessageInformation, Exception> MessageProcessingFailure;
        public event Action<CurrentMessageInformation, Exception> MessageProcessingCompleted;
        public event Action<CurrentMessageInformation> BeforeMessageTransactionRollback;
        public event Action<CurrentMessageInformation> BeforeMessageTransactionCommit;
        public event Action<CurrentMessageInformation, Exception> AdministrativeMessageProcessingCompleted;
        public event Action Started;
    }
}
