using System;

namespace Rhino.Files.Internal
{
    public class ResourceSession : ResourceBase
    {
        Guid _sessionId;

        public ResourceSession(Guid id)
        {
            _sessionId = id;
            // BEGIN-SESSION
            base.ResourceWasAllocated();
        }

        public void End()
        {
            base.CheckObjectIsNotDisposed();
            ReleaseResource();
        }

        protected override void ReleaseResource()
        {
            if (_sessionId != Guid.Empty)
            {
                // END-SESSION
            }
            _sessionId = Guid.Empty;
            base.ResourceWasReleased();
        }

        public Guid SessionId
        {
            get { base.CheckObjectIsNotDisposed(); return _sessionId; }
        }

        public static implicit operator Guid(ResourceSession session)
        {
            return session.SessionId;
        }
    }
}
