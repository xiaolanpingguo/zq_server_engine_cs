using MemoryPack;

namespace ZQ.S2S
{
    [MemoryPackable]
    public partial class ServerInfo
    {
        public string ServerId { get; set; } = null!;
        public short ServerType { get; set; }
        public int PlayerNum { get; set; }
        public string IP { get; set; } = null!;
        public ushort Port { get; set; }
    }

    [MemoryPackable]
    public partial class S2M_ServerInfoReportReq
    {
        public ServerInfo data { get; set; } = null!;
    }

    [MemoryPackable]
    public partial class S2M_ServerInfoReportRes
    {
        public S2SErrorCode ErrorCode { get; set; }
    }
}
