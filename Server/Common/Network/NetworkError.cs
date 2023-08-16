namespace ZQ
{
    public static class NetworkError
    {
        public const int ERR_SocketError = 1;
        public const int ERR_PeerDisconnect = 2;
        public const int ERR_TcpChannelRecvError = 3;
        public const int ERR_TcpChannelSendError = 4;
        public const int ERR_CloseByServer = 5;
        public const int ERR_ErrorMessageId = 6;
        public const int ERR_SessionTimeout = 7;
    }
}