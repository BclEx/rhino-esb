﻿using Rhino.Files.Exceptions;
using Rhino.Files.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Rhino.Files.Storage
{
    public class AbstractActions : IDisposable
    {
        readonly Dictionary<string, QueueActions> _queuesByName = new Dictionary<string, QueueActions>();
        protected readonly string _database;
        protected readonly Guid _instanceId;
        //protected ResourceSession _session;
        //protected ResourceTransaction _transaction;

        public AbstractActions(string database, Guid instanceId)
        {
            _database = database;
            _instanceId = instanceId;
            try
            {
                //_session = new ResourceSession(instanceId);
                //_transaction = new ResourceTransaction(_session);
            }
            catch (Exception) { Dispose(); throw; }
        }

        public void Dispose()
        {
            try
            {
                foreach (var action in _queuesByName.Values)
                    action.Dispose();
                //if (_transaction != null)
                //    _transaction.Dispose();
            }
            catch (Exception e) { Trace.WriteLine(e.ToString()); Debugger.Break(); }
        }

        public QueueActions GetQueue(string queueName)
        {
            QueueActions actions;
            if (_queuesByName.TryGetValue(queueName, out actions))
                return actions;
            var path = Path.Combine(_database, queueName);
            if (!Directory.Exists(path))
                throw new QueueDoesNotExistsException(queueName);
            _queuesByName[queueName] = actions = new QueueActions(_database, _instanceId, queueName, GetSubqueues(queueName), this, i => AddToNumberOfMessagesIn(queueName, i));
            return actions;
        }

        private string[] GetSubqueues(string queueName)
        {
            var path = Path.Combine(_database, queueName);
            if (!Directory.Exists(path))
                return new string[0];
            return Directory.EnumerateDirectories(path).ToArray();
        }

        public void AddSubqueueTo(string queueName, string subQueue)
        {
            var path = Path.Combine(_database, queueName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private void AddToNumberOfMessagesIn(string queueName, int count)
        {
        }

        public void Commit()
        {
            //_transaction.Commit();
        }
    }
}

