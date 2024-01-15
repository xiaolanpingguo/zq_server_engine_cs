namespace ZQ
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TestServer server = new TestServer();
            if (!server.Init(args))
            {
                Console.WriteLine("init server failed!");
                return;
            }

            server.Run();
        }
    }
}