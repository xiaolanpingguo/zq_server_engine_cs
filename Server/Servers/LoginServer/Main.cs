namespace ZQ
{
    internal class Program
    {
        static void Main(string[] args)
        {
            LoginServer server = new LoginServer();
            if (!server.Init(args))
            {
                Console.WriteLine("init server failed!");
                return;
            }

            server.Run();
        }
    }
}