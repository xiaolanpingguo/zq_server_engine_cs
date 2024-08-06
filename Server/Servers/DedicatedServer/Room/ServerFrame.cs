using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ZQ
{
    public struct PlayerInput
    {
        public int Horizontal;
        public int Vertical;
        public int Button;
        public static PlayerInput EmptyInput = new PlayerInput
        {
            Horizontal = 0,
            Vertical = 0,
            Button = 0,
        };
    }

    public class ServerFrame
    {
        public Dictionary<string, PlayerInput> Inputs = new();
        public int Tick;
    }
}
