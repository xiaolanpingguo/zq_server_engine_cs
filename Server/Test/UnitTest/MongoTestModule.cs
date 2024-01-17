using MongoDB.Bson.Serialization.Attributes;
using ZQ.Mongo;

namespace ZQ
{
    public class MongoTestModule : IModule
    {
        [BsonIgnoreExtraElements]
        public class TestA
        {
            public string indexId { get; set; } = null!;
            public string zq { get; set; } = null!;
            public int code { get; set; }
            public string zq1 { get; set; } = null!;
        }

        const string s_dbName = "zq";
        const string s_colName = "mytest";

        private MongoModule m_mongo;
        public MongoTestModule()
        {
            List<MongoDBSetupConfig.DBInfo> dbs = new()
            {
                 new MongoDBSetupConfig.DBInfo
                 {
                     dbName = s_dbName,
                     collections = new MongoDBSetupConfig.CollectionInfo[]{
                         new MongoDBSetupConfig.CollectionInfo()
                         {
                             collectionName = s_colName,
                             indexNames = new string[]{ "indexId"}

                         },
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

            m_mongo = new MongoModule(mongoConfig);
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
                indexId = "123",
                zq = "456",
                code = 999
            };

            Console.WriteLine($"will test: MongoInsert");
            MongoResult<object> result = await m_mongo.Insert<TestA>(s_dbName, s_colName, new List<TestA> { aa });
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
            MongoResult<object> result = await m_mongo.Delete(s_dbName, s_colName, "indexId", "123");
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
            MongoResult<TestA> result = await m_mongo.Find<TestA>(s_dbName, s_colName, "indexId", "123");
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                return;
            }

            Console.WriteLine($"test mongo find success, result:\n");
            foreach (TestA a in result.Result)
            {
                Console.WriteLine($"{a.indexId} : {a.zq} : {a.code}");
            }
        }

        private async Task TestSave()
        {
            TestA aa = new TestA
            {
                indexId = "123",
                zq = "456",
                code = 888,
                zq1 = "dwadaw"
            };

            Console.WriteLine($"will test mongo insert");
            MongoResult<TestA> result = await m_mongo.Save<TestA>(s_dbName, s_colName, "indexId", "123", aa);
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                return;
            }

            Console.WriteLine($"test mongo save success, result:\n");
            foreach (TestA a in result.Result)
            {
                Console.WriteLine($"{a.indexId} : {a.zq} : {a.code}");
            }
        }

        private async Task TestUpdate()
        {
            TestA aa = new TestA
            {
                indexId = "123",
                zq = "456",
                code = 888,
                zq1 = "dwadaw"
            };
            Console.WriteLine($"will test mongo update");
            MongoResult<object> result = await m_mongo.Update(s_dbName, s_colName, "indexId", "123", "zq1", aa);
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