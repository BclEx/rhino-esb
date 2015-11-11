using Rhino.Files.Model;
using Rhino.Files.Utils;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.CompilerServices;

namespace Rhino.Files.Protocol
{

    public static class SerializationExtensions
    {
        public static byte[] Serialize(this Message[] messages)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(messages.Length);
                foreach (var message in messages)
                {
                    w.Write(message.Id.SourceInstanceId.ToByteArray());
                    w.Write(message.Id.MessageIdentifier.ToByteArray());
                    w.Write(message.Queue);
                    w.Write(message.SubQueue ?? string.Empty);
                    w.Write(message.SentAt.ToBinary());
                    w.Write(message.Headers.Count);
                    foreach (string str in message.Headers)
                    {
                        w.Write(str);
                        w.Write(message.Headers[str]);
                    }
                    w.Write(message.Data.Length);
                    w.Write(message.Data);
                }
                w.Flush();
                return stream.ToArray();
            }
        }

        public static Message[] ToMessages(byte[] buffer)
        {
            using (var s = new MemoryStream(buffer))
            using (var r = new BinaryReader(s))
            {
                var num = r.ReadInt32();
                var messages = new Message[num];
                for (int i = 0; i < num; i++)
                {
                    messages[i] = new Message
                    {
                        Id = new MessageId
                        {
                            SourceInstanceId = new Guid(r.ReadBytes(0x10)),
                            MessageIdentifier = new Guid(r.ReadBytes(0x10))
                        },
                        Queue = r.ReadString(),
                        SubQueue = r.ReadString(),
                        SentAt = DateTime.FromBinary(r.ReadInt64()),
                    };
                    var capacity = r.ReadInt32();
                    messages[i].Headers = new NameValueCollection(capacity);
                    for (var j = 0; j < capacity; j++)
                        messages[i].Headers.Add(r.ReadString(), r.ReadString());
                    int count = r.ReadInt32();
                    messages[i].Data = r.ReadBytes(count);
                    if (string.IsNullOrEmpty(messages[i].SubQueue))
                        messages[i].SubQueue = null;
                }
                return messages;
            }
        }

        //public static string ToQueryString(this NameValueCollection qs)
        //{
        //    return string.Join("&", Array.ConvertAll<string, string>(qs.AllKeys, key => string.Format("{0}={1}", MonoHttpUtility.UrlEncode(key), MonoHttpUtility.UrlEncode(qs[key]))));
        //}
    }
}

