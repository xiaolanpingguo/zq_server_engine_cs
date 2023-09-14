namespace ZQ
{
    public static class NetworkError
    {
        // for TCP
        public const int ERR_SocketError = 1;
        public const int ERR_PeerDisconnect = 2;
        public const int ERR_TcpChannelRecvError = 3;
        public const int ERR_TcpChannelSendError = 4;
        public const int ERR_CloseByServer = 5;
        public const int ERR_ErrorMessageId = 6;
        public const int ERR_SessionTimeout = 7;

        // for KCP
        public const int ERR_KcpSplitCountError = 31;
        public const int ERR_KcpReadNotSame = 32;
        public const int ERR_KcpSplitError = 33;
        public const int ERR_KcpSocketError = 34;
        public const int ERR_KcpConnectTimeout = 35;
        public const int ERR_KcpSocketCantSend = 36;
        public const int ERR_KcpWaitSendSizeTooLarge = 37;
        public const int ERR_KcpAcceptTimeout = 38;
        public const int ERR_KcpNotFoundChannel = 39;
        public const int ERR_KcpPacketSizeError = 40;
        public const int ERR_KcpDeserializePacketSizeError = 41;
    }
}