using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using ZQ.Mongo;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Drawing;
using MongoDB.Bson.Serialization;
using static ZQ.DBOperator;

namespace ZQ
{
    [BsonIgnoreExtraElements]
    public class DBBaseItem
    {
        public string Id;
        public int Count;
    }

    [BsonIgnoreExtraElements]
    public class DBOperator : DBBaseItem
    {
        public class DBLoadoutsData
        {
            public string OperatorId;
            public int Category;
            public int DomainRarity;
        }

        public int TrackLevel;
        public int TrackExp;
        public List<DBLoadoutsData> Loadouts;
    }

    [BsonIgnoreExtraElements]
    public class DBBattlePass
    {
        public bool Active;
        public int TrackLevel;
        public int TrackExp;
    }

    [BsonIgnoreExtraElements]
    public class DBPlayerInventory
    {
        public Dictionary<string, DBBaseItem> BaseItems;
        public Dictionary<string, DBOperator> Operators;
        public DBBattlePass BattlePass;
    }

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

        [BsonIgnoreExtraElements]
        public class TestBsonBin
        {
            public string indexId { get; set; } = null!;
            public string profileId { get; set; } = null!;
            public byte[] bin { get; set; } = null!;
        }

        [BsonIgnoreExtraElements]
        public class TestBsonBin1
        {
            public string indexId { get; set; } = null!;
            public string profileId { get; set; } = null!;
            public TestA bin { get; set; } = null!;
        }

        const string s_dbName = "zq";
        const string s_colName = "mytest";
        const string s_bsonColName = "bsontest";

        private IMongoCollection<BsonDocument> m_bsonCollection;

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
                         new MongoDBSetupConfig.CollectionInfo()
                         {
                             collectionName = s_bsonColName
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

            string url = "mongodb://127.0.0.1:27017";
            var mongoClient = new MongoClient(url);
            var mongodatabase = mongoClient.GetDatabase(s_dbName);
            m_bsonCollection = mongodatabase.GetCollection<BsonDocument>(s_bsonColName);
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
            if (key.Key == ConsoleKey.D6)
            {
                TestBson().FireAndForget();
            }
            if (key.Key == ConsoleKey.D7)
            {
                TestInsertPlayerInventory().FireAndForget();
            }
        }

        private async Task TestBson()
        {
            try
            {
                TestA testA = new TestA()
                {
                    indexId = "12345678912345678912xy",
                    zq = "45678912312345678912x",
                    code = 23133,
                    zq1 = "78912345612345678912zzz",
                };

                TestBsonBin aa = new TestBsonBin
                {
                    indexId = "456",
                    profileId = "dwadwadwa",
                    bin = BsonSerializeHelper.Serialize(testA),
                };


                await m_mongo.Insert<TestBsonBin>(s_dbName, s_bsonColName, new List<TestBsonBin> { aa });
                MongoResult<TestBsonBin> result = await m_mongo.Find<TestBsonBin>(s_dbName, s_bsonColName, "indexId", "123");
                if (!result.Success)
                {
                    Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                    return;
                }

                if (result.Result.Count > 0)
                {
                    byte[] desTest = result.Result[0].bin;
                    TestA desaa = BsonSerializer.Deserialize<TestA>(desTest);
                    Console.WriteLine($"TestBson find success, result:{desaa.ToJson().ToString()}");
                }
 

                Console.WriteLine($"TestBson success.");
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"TestBson failed, ex:{ex}");
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

        private async Task TestInsertPlayerInventory()
        {
            Console.WriteLine($"will test: TestInsertPlayerInventory");

            DBPlayerInventory inventory = new DBPlayerInventory();
            {
                inventory.BattlePass = new DBBattlePass();
                inventory.BattlePass.TrackLevel = 1;
                inventory.BattlePass.Active = false;
                inventory.BattlePass.TrackExp = 100;
            }
            {
                inventory.BaseItems = new();
                DBBaseItem item1 = new DBBaseItem();
                item1.Count = 1;
                item1.Id = "item1";
                inventory.BaseItems.Add("item1", item1);
            }
            {
                inventory.Operators = new();

                DBOperator operator1 = new DBOperator();
                operator1.Id = "operator1";
                operator1.Count = 1;
                operator1.TrackExp = 100;
                operator1.TrackLevel = 1;

                operator1.Loadouts = new List<DBLoadoutsData>();
                DBOperator.DBLoadoutsData loadout1 = new();
                loadout1.Category = 1;
                loadout1.DomainRarity = 2;
                loadout1.OperatorId = "operator1";

                operator1.Loadouts.Add(loadout1);
                inventory.Operators.Add(operator1.Id, operator1);
            }

            MongoResult<object> result = await m_mongo.Insert<DBPlayerInventory>(s_dbName, s_colName, new List<DBPlayerInventory> { inventory });
            if (!result.Success)
            {
                Console.WriteLine($"mongo failed:{result.ErrorDesc}");
                return;
            }
            else
            {
                Console.WriteLine($"test mongo TestInsertPlayerInventory success.");
            }
        }
    }
}