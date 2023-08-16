using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Net;
using System.Threading;


namespace ZQ
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Game game = Game.Instance;
            if (!game.Init(args))
            {
                Console.WriteLine("init game failed!");
                return;
            }

            game.Update();
        }
    }
}