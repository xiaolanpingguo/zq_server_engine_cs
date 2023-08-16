using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZQ.Redis
{

    public static class RedisDefines
    {
        private static string sessionPrefix = "session@"; 

        public static string GetPlayerSessionKey(string profileId)
        {
            return $"{sessionPrefix}{profileId}";
        }
    }

    [MemoryPackable]
    public partial class RedisPlayerSession
    {
        public string SDKUserId { get; set; }
        public string SDKToken { get; set; }
        public int SDKChannelId { get; set; }
        public string ProfileId { get; set; }
        public string ZoneServerId { get; set; }
        public string ZoneIP { get; set; }
        public ushort ZonePort { get; set; }
    }
}
