using MemoryPack;

namespace ZQ.S2S
{
    [MemoryPackable]
    public partial class L2M_SuitableZoneReq
    {
    }

    [MemoryPackable]
    public partial class L2M_SuitableZoneRes
    {
        public S2SErrorCode ErrorCode { get; set; }
        public ServerInfo data { get; set; } = null!;
    }

    [MemoryPackable]
    public partial class L2M_AllZoneServersReq
    {
    }

    [MemoryPackable]
    public partial class L2M_AllZoneServersRes
    {
        public S2SErrorCode ErrorCode { get; set; }
        public List<ServerInfo> ZoneServers { get; set; } = null!;
    }
}
