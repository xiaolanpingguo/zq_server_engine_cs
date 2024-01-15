namespace ZQ
{
    internal class Program
    {
        static void Main(string[] args)
        {
            LobbyServer server = new LobbyServer();
            if (!server.Init(args))
            {
                Console.WriteLine("init server failed!");
                return;
            }

            server.Run();
        }
    }
}