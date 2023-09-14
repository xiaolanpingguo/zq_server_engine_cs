using Google.Protobuf;
using MemoryPack;

namespace ZQ
{
    public enum C2DSMessageId : ushort
    {
        C2DS_LoadingProgress = 501,
        C2DS_FrameMessage = 502,
        DS2C_AdjustUpdateTime = 503,
        DS2C_CheckHashFail = 504,
        DS2C_OneFrameInputs = 505,

        C2DS_PingReq = 506,
        C2DS_PingRes = 507,
    }

    [MemoryPackable]
    public partial class C2DS_PlayerMatchReq
    {
    }

    [MemoryPackable]
    public partial class C2DS_LoadingProgressReq
    {
        public int Progress { get; set; }
    }

    [MemoryPackable]
    public partial class DS2C_Start
    {
        public long StartTime { get; set; }
        //public List<LockStepUnitInfo> UnitInfo { get; set; } = new();
    }

    [MemoryPackable]
    public partial struct EntityInput
    {
        public TrueSync.TSVector2 V;
        public int Button;

        public bool Equals(EntityInput other)
        {
            return this.V == other.V && this.Button == other.Button;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityInput other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.V, this.Button);
        }

        public static bool operator ==(EntityInput a, EntityInput b)
        {
            if (a.V != b.V)
            {
                return false;
            }

            if (a.Button != b.Button)
            {
                return false;
            }

            return true;
        }

        public static bool operator !=(EntityInput a, EntityInput b)
        {
            return !(a == b);
        }
    }

    [MemoryPackable]
    public partial class OneFrameInputs
    {
        public Dictionary<string, EntityInput> Inputs = new();
    }

    [MemoryPackable]
    public partial class DS2C_OneFrameInputs
    {
        public OneFrameInputs FrameInputs{ get; set; }
    }

    [MemoryPackable]
    public partial class C2DS_FrameMessage
    {
        public int Frame { get; set; }
        public string ProfileId { get; set; }
        public EntityInput Input { get; set; }
    }


    [MemoryPackable]
    public partial class DS2C_AdjustUpdateTime
    {
        public int DiffTime { get; set; }
    }


    [MemoryPackable]
    public partial class DS2C_CheckHashFail
    {
        public int Frame { get; set; }
        public byte[] WorldBytes { get; set; }
    }


    [MemoryPackable]
    public partial class C2DS_PingReq
    {
    }

    [MemoryPackable]
    public partial class C2DS_PingRes
    {
        public long Time { get; set; }
    }
}
