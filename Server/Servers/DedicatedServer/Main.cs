using System;

namespace ZQ
{
    internal class Program
    {
        static void Main(string[] args)
        {
            DedicatedServer server = new DedicatedServer();
            if (!server.Init(args))
            {
                Console.WriteLine("init server failed!");
                return;
            }

            server.Run();
        }
    }
}