namespace ZQ
{
    public partial class Kcp
    {
        public const int k_receiveBufferSize = 1024 * 1024 * 4;
        public const int k_sendBufferSize = 1024  * 1024 * 4;

        public struct SegmentHead
        {
            public uint conv;     
            public byte cmd;
            public byte frg;
            public ushort wnd;      
            public uint ts;     
            public uint sn;       
            public uint una;
            public uint len;
        }
    }
    
    
}