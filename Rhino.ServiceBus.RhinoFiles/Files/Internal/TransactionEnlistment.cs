using Common.Logging;
using Rhino.Files.Storage;
using System;
using System.Transactions;

namespace Rhino.Files.Internal
{
    internal class TransactionEnlistment : ISinglePhaseNotification, IEnlistmentNotification
    {
        readonly Action _assertNotDisposed;
        readonly ILog _logger = LogManager.GetLogger(typeof(TransactionEnlistment));
        readonly Action _onCompelete;
        readonly QueueStorage _queueStorage;

        public TransactionEnlistment(QueueStorage queueStorage, Action onCompelete, Action assertNotDisposed)
        {
            _queueStorage = queueStorage;
            _onCompelete = onCompelete;
            _assertNotDisposed = assertNotDisposed;
            var current = Transaction.Current;
            if (current != null)
                current.EnlistDurable(queueStorage.Id, (ISinglePhaseNotification)this, EnlistmentOptions.None);
            Id = Guid.NewGuid();
            _logger.DebugFormat("Enlisting in the current transaction with enlistment id: {0}", Id);
        }

        public Guid Id { get; private set; }

        public void Commit(Enlistment enlistment)
        {
            try { PerformActualCommit(); enlistment.Done(); }
            catch (Exception e) { _logger.Warn("Failed to commit enlistment " + Id, e); }
            finally { _onCompelete(); }
        }

        public void InDoubt(Enlistment enlistment)
        {
            enlistment.Done();
        }

        private void PerformActualCommit()
        {
            _assertNotDisposed();
            _logger.DebugFormat("Committing enlistment with id: {0}", Id);
            _queueStorage.Global(actions =>
            {
                actions.RemoveReversalsMoveCompletedMessagesAndFinishSubQueueMove(Id);
                actions.MarkAsReadyToSend(Id);
                actions.DeleteRecoveryInformation(Id);
                actions.Commit();
            });
            _logger.DebugFormat("Commited enlistment with id: {0}", Id);
        }

        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            _assertNotDisposed();
            _logger.DebugFormat("Preparing enlistment with id: {0}", Id);
            var information = preparingEnlistment.RecoveryInformation();
            _queueStorage.Global(actions =>
            {
                actions.RegisterRecoveryInformation(Id, information);
                actions.Commit();
            });
            preparingEnlistment.Prepared();
            _logger.DebugFormat("Prepared enlistment with id: {0}", Id);
        }

        public void Rollback(Enlistment enlistment)
        {
            try
            {
                _assertNotDisposed();
                _logger.DebugFormat("Rolling back enlistment with id: {0}", Id);
                _queueStorage.Global(actions =>
                {
                    actions.ReverseAllFrom(Id);
                    actions.DeleteMessageToSend(Id);
                    actions.Commit();
                });
                enlistment.Done();
                _logger.DebugFormat("Rolledback enlistment with id: {0}", Id);
            }
            catch (Exception e) { _logger.Warn("Failed to rollback enlistment " + Id, e); }
            finally { _onCompelete(); }
        }

        public void SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            try { PerformActualCommit(); singlePhaseEnlistment.Done(); }
            catch (Exception e) { _logger.Warn("Failed to commit enlistment " + Id, e); }
            finally { _onCompelete(); }
        }
    }
}

