using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Anabasis.EventStore;

namespace Anabasis.Tests.Demo
{
    public class ProtoBuffSerializer : ISerializer
    {
        public object DeserializeObject(byte[] bytes, Type type)
        {
            using (var ms = new MemoryStream(bytes))
            {
                return Serializer.Deserialize(type, ms);
            }  
        }

        public byte[] SerializeObject(object obj)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, obj);

                return ms.ToArray();
            }
        }
    }
}
