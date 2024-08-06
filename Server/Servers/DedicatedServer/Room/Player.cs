using C2S;
using Google.Protobuf;
using System.Xml.Linq;
using ZQ.Mongo;


namespace ZQ
{
    public class Player
    {
        public int ConnectionId { get; set; }
        public string ProfileId { get; set; } = null!;
        public string IP { get; set; } = null!;
    }
}
