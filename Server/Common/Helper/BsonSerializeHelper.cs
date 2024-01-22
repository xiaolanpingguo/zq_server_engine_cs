using System;
using System.ComponentModel;
using System.IO;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace ZQ
{
    public static class BsonSerializeHelper
    {
        public static byte[] Serialize<T>(T obj)
        {
            try
            {
                // equal: 
                //using (var memoryStream = new MemoryStream())
                //{
                //    using (var bsonWriter = new BsonBinaryWriter(memoryStream, BsonBinaryWriterSettings.Defaults))
                //    {
                //        var context = BsonSerializationContext.CreateRoot(bsonWriter);
                //        serializer.Serialize(context, args, obj);
                //    }
                //    return memoryStream.ToArray();
                //}
                return obj.ToBson();
            }
            catch(Exception e) 
            {
                Log.Error($"BsonSerializer:Serialize exception, type:{obj.GetType()}, ex:{e}");
                return null;
            }
        }

        public static T Deserialize<T>(byte[] bytes) where T : class
        {
            try
            {
                return BsonSerializer.Deserialize<T>(bytes);
            }
            catch (Exception e)
            {
                Log.Error($"BsonSerializer:Deserialize<T> exception, type:{typeof(T)}, ex:{e}");
                return null;
            }
        }

        public static object Deserialize(Type type, byte[] bytes)
        {
            try
            {
                return BsonSerializer.Deserialize(bytes, type);
            }
            catch (Exception e)
            {
                Log.Error($"BsonSerializer:Deserialize exception, type:{type.GetType()}, ex:{e}");
                return null;
            }
        }
    }
}