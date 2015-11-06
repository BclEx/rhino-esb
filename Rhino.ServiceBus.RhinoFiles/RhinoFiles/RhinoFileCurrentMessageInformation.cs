using Rhino.Files;
using Rhino.Files.Model;
using Rhino.ServiceBus.Impl;
using System;

namespace Rhino.ServiceBus.RhinoFiles
{
    [CLSCompliant(false)]
    public class RhinoFileCurrentMessageInformation : CurrentMessageInformation
    {
        public Uri ListenUri { get; set; }
        public IQueue Queue { get; set; }
        public Message TransportMessage { get; set; }
    }
}