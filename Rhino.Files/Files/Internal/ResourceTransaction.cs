using System;

namespace Rhino.Files.Internal
{
    public class ResourceTransaction : ResourceBase
    {
        Guid _sessionId;

        public ResourceTransaction(Guid sessionId)
        {
            _sessionId = sessionId;
            Begin();
        }

        public void Begin()
        {
            base.CheckObjectIsNotDisposed();
            if (IsInTransaction)
                throw new InvalidOperationException("Already in a transaction");
            //Api.JetBeginTransaction(_sessionId);
            base.ResourceWasAllocated();
        }

        public void Commit()
        {
            base.CheckObjectIsNotDisposed();
            if (!IsInTransaction)
                throw new InvalidOperationException("Not in a transaction");
            //Api.JetCommitTransaction(_sessionId, grbit);
            base.ResourceWasReleased();
        }

        protected override void ReleaseResource()
        {
            Rollback();
        }

        public void Rollback()
        {
            base.CheckObjectIsNotDisposed();
            if (!IsInTransaction)
                throw new InvalidOperationException("Not in a transaction");
            //Api.JetRollback(_sessionId, RollbackTransactionGrbit.None);
            base.ResourceWasReleased();
        }

        public bool IsInTransaction
        {
            get { base.CheckObjectIsNotDisposed(); return base.HasResource; }
        }
    }
}
