using C2S;
using Google.Protobuf;
using System.Xml.Linq;
using ZQ.Mongo;

namespace ZQ
{
    public class AuthorityPlayer
    {
        public ulong ChannelId { get; set; }
        public string ProfileId { get; set; }
        public string IP { get; set; }
        public int Progress { get; set; }

        public AuthorityPlayer()
        {
        }
    }
}
