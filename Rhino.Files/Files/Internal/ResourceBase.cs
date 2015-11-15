using System;

namespace Rhino.Files.Internal
{
    public abstract class ResourceBase : IDisposable
    {
        bool _hasResource;
        bool _isDisposed;

        protected void CheckObjectIsNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("Resource");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_hasResource)
                    ReleaseResource();
                _isDisposed = true;
            }
        }

        ~ResourceBase() { Dispose(false); }

        protected abstract void ReleaseResource();

        protected void ResourceWasAllocated()
        {
            CheckObjectIsNotDisposed();
            _hasResource = true;
        }

        protected void ResourceWasReleased()
        {
            CheckObjectIsNotDisposed();
            _hasResource = false;
        }

        protected bool HasResource
        {
            get { return _hasResource; }
        }
    }
}

