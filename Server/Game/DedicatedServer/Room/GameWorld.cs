using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using TrueSync;

namespace ZQ
{
    public class GameWorld
    {
        public GameWorld()
        {
        }

        public TSRandom Random { get; set; }

        
        public int Frame { get; set; }

        public void Update()
        {
            ++this.Frame;
        }
    }
}