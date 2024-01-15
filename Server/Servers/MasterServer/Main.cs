namespace ZQ
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MasterServer server = new MasterServer();
            if (!server.Init(args))
            {
                Console.WriteLine("init server failed!");
                return;
            }

            server.Run();
        }
    }
}