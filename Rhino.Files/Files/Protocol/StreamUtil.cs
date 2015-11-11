using System;
using System.Collections.Generic;
using System.IO;
using Wintellect.Threading.AsyncProgModel;

namespace Rhino.Files.Protocol
{
    public class StreamUtil
    {
        public static IEnumerator<int> ReadBytes(byte[] buffer, Stream stream, AsyncEnumerator ae, string type, bool expectedToHaveNoData)
        {
            int offset = 0;
        Label_PostSwitchInIterator:
            if (offset < buffer.Length)
            {
                stream.BeginRead(buffer, offset, buffer.Length - offset, ae.End(), null);
                yield return 1;
                var i = stream.EndRead(ae.DequeueAsyncResult());
                if (i == 0)
                {
                    if (!expectedToHaveNoData)
                        throw new InvalidOperationException("Could not read value for " + type);
                }
                else
                {
                    offset += i;
                    goto Label_PostSwitchInIterator;
                }
            }
        }

    }
}

