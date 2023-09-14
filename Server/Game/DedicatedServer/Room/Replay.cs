using System.Collections.Generic;
using MemoryPack;

namespace ZQ
{
    [MemoryPackable]
    public partial class Replay
    {
        //public List<LockStepUnitInfo> UnitInfos;
        public List<OneFrameInputs> FrameInputs = new();
        public List<byte[]> Snapshots = new();
    }
}