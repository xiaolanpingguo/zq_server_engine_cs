using MemoryPack;

namespace ZQ.S2S
{
    public enum S2SMessageId : ushort
    {
        HEARTBEAT = 101,

        // Server->Master
        S2M_SERVER_REPORT_REQ = 150,
        S2M_SERVER_REPORT_RES = 151,

        // Login->Master
        L2M_SUITABLE_ZONE_REQ = 200,
        L2M_SUITABLE_ZONE_RES = 201,
        L2M_ALL_ZONE_SERVERS_REQ = 202,
        L2M_ALL_ZONE_SERVERS_RES = 203,

        // Login->ZoneManager
        L2ZM_SERVER_REPORT_REQ = 250,
        L2ZM_SERVER_REPORT_RES = 251,
        L2ZM_LOGIN_REQ = 252,
        L2ZM_LOGIN_RES = 253,

        // Zone->ZoneManager
        Z2ZM_SERVER_REPORT_REQ = 300,
        Z2ZM_SERVER_REPORT_RES = 301,
        Z2ZM_LOGIN_REQ = 302,
        Z2ZM_LOGIN_RES = 303,
    }

    public enum S2SErrorCode : int
    {
        SUCCESS = 0,
        GENERRAL_ERROR = 10001,
        SERVER_INTERNAL_ERROR = 10002,
        SERVER_NOT_READY = 10003,
        INVALID_PARAMETER = 10004,
        SERVER_BUSY = 10005,

        // login
        LOGIN_IN_PROGRESS = 10006,
        LOGIN_SESSION_HAS_EXPIRED = 10007,
        LOGIN_LOAD_DATA_FAILED = 10008,
    }

    [MemoryPackable]
    public partial class S2S_ServerHeartBeat
    {
    }
}
