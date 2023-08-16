using MongoDB.Bson.Serialization.Attributes;
using ZQ.Mongo;

namespace ZQ
{
    public class MongoTestComponent : IComponent
    {
        [BsonIgnoreExtraElements]
        public class TestA
        {
            public string abc { get; set; }
            public string zq { get; set; }
            public int code { get; set; }
            public string zq1 { get; set; }
        }


        public class AA
        {
            public string profile_id { get; set; }
        }

        private MongoComponent m_mongo;
        public MongoTestComponent()
        {
            List<MongoDBSetupConfig.DBInfo> dbs = new()
            {
                 new MongoDBSetupConfig.DBInfo
                 {
                     dbName = "zq",
                     collections = new MongoDBSetupConfig.CollectionInfo[]{
                         new MongoDBSetupConfig.CollectionInfo()
                         {
                             collectionName = "accounts",
                             indexNames = new string[]{ "SDKUserId", "ProfileId"}

                         },
                         new MongoDBSetupConfig.CollectionInfo()
                         {
                             collectionName = "players",
                             indexNames = new string[]{ "ProfileId"}
                         }
                     }
                 }
            };
            MongoDBSetupConfig mongoConfig = new MongoDBSetupConfig()
            {
                ip = "127.0.0.1",
                port = 27017,
                srv = "",
                user = "",
                pwd = "",
                dbs = dbs.ToArray()
            };

            m_mongo = new MongoComponent(mongoConfig);
        }

        public bool Init()
        {
            m_mongo.Init();
            return true;
        }

        public bool Update(long timeNow)
        {
            m_mongo.Update(timeNow);
            Test();
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        private void Test()
        {
            if (!Console.KeyAvailable)
            {
                return;
            }

            ConsoleKeyInfo key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.D1)
            {
                TestInsert().FireAndForget();
            }
            if (key.Key == ConsoleKey.D2)
            {
                TestDelete().FireAndForget();
            }
            if (key.Key == ConsoleKey.D3)
            {
                TestFind().FireAndForget();
            }
            if (key.Key == ConsoleKey.D4)
            {
                TestSave().FireAndForget();
            }
            if (key.Key == ConsoleKey.D5)
            {
                TestUpdate().FireAndForget();
            }
        }

        private async Task TestInsert()
        {
            TestA aa = new TestA
            {
                abc = "333",
                zq = "456",
                code = 999
            };

            Console.WriteLine($"will test: MongoInsert");
            MongoResult<object> result = await m_mongo.Insert<TestA>("zq", "account", new List<TestA> { aa });
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                return;
            }
            else
            {
                Console.WriteLine($"test mongo insert success.");
            }
        }

        private async Task TestDelete()
        {
            Console.WriteLine($"will test: TestDelete");
            MongoResult<object> result = await m_mongo.Delete("zq", "account", "abc", "333");
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
            }
            else
            {
                Console.WriteLine($"test mongo delete success.");
            }
        }

        private async Task TestFind()
        {
            Console.WriteLine($"will test: TestFind");
            MongoResult<TestA> result = await m_mongo.Find<TestA>("zq", "account", "abc", "333");
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                return;
            }

            Console.WriteLine($"test mongo find success, result:\n");
            foreach (TestA a in result.Result)
            {
                Console.WriteLine($"{a.abc} : {a.zq} : {a.code}");
            }
        }

        private async Task TestSave()
        {
            TestA aa = new TestA
            {
                abc = "123",
                zq = "456",
                code = 888,
                zq1 = "dwadaw"
            };

            var playerData = new DBPlayerData();
            var gid = "0b3dae14-09d9-4b75-9b4c-5f0a2a49a603";
            playerData.ProfileId = gid;

            Console.WriteLine($"will test mongo insert");
            MongoResult<DBPlayerData> result = await m_mongo.Save<DBPlayerData>("zq", "players", "ProfileId", gid, playerData);
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                return;
            }

            Console.WriteLine($"test mongo save success, result:\n");
            foreach (DBPlayerData a in result.Result)
            {
                //Console.WriteLine($"{a.abc} : {a.zq} : {a.code}");
            }
        }

        private async Task TestUpdate()
        {
            TestA aa = new TestA
            {
                abc = "123",
                zq = "456",
                code = 888,
                zq1 = "dwadaw"
            };
            Console.WriteLine($"will test mongo update");
            MongoResult<object> result = await m_mongo.Update("zq", "account", "abc", "123", "zq2", aa);
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                return;
            }
            else
            {
                Console.WriteLine($"test mongo update success.");
            }
        }
    }
}