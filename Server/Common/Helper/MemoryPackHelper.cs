using System;
using System.ComponentModel;
using MemoryPack;

namespace ZQ
{
    public static class MemoryPackHelper
    {
        public static byte[] Serialize(object obj)
        {
            try
            {
                return MemoryPackSerializer.Serialize(obj.GetType(), obj);
            }
            catch(Exception e) 
            {
                Log.Error($"MemoryPackHelper:Serialize exception, type:{obj.GetType()}, ex:{e}");
                return null;
            }
        }

        public static T Deserialize<T>(byte[] bytes) where T : class
        {
            try
            {
                return MemoryPackSerializer.Deserialize<T>(bytes);
            }
            catch (Exception e)
            {
                Log.Error($"MemoryPackHelper:Deserialize<T> exception, type:{typeof(T)}, ex:{e}");
                return null;
            }
        }

        public static object Deserialize(Type type, byte[] bytes)
        {
            try
            {
                return MemoryPackSerializer.Deserialize(type, bytes);
            }
            catch (Exception e)
            {
                Log.Error($"MemoryPackHelper:Deserialize exception, type:{type.GetType()}, ex:{e}");
                return null;
            }
        }
    }
}