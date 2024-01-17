using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Threading.Tasks;


namespace ZQ
{
    public class MongoDBSetupConfig
    {
        public class DBInfo
        {
            public string dbName { get; set; }
            public CollectionInfo[] collections { get; set; }
        }
        public class CollectionInfo
        {
            public string collectionName { get; set; }
            public string[] indexNames { get; set; }
        }
        public string ip { get; set; }
        public ushort port { get; set; }
        public string srv { get; set; }
        public string user { get; set; }
        public string pwd { get; set; }
        public DBInfo[] dbs { get; set; }
    }

    public class MongoResult<T> where T : class
    {
        public bool Success;
        public string ErrorDesc;
        public List<T> Result;
    }

    public class MongoModule : IModule
    {
        private string m_connectionUrl;
        private MongoDBSetupConfig m_config;
        private MongoClient m_client;

        private Dictionary<string, Dictionary<string, IMongoCollection<BsonDocument>>> m_collections = new();

        public MongoModule(MongoDBSetupConfig config)
        {
            m_config = config;
        }

        public bool Init()
        {
            if (!Setup())
            {
                return false;
            }

            return true;
        }

        public bool Update(long timeNow)
        {
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        public async Task<MongoResult<object>> Insert<T>(string dbName, string collName, List<T> obj) where T : class
        {
            try
            {
                var col = GetCollection(dbName, collName);
                if (col == null)
                {
                    string str = $"[Insert] cannot find collection, db:{dbName}, col:{collName}";
                    Log.Error(str);
                    return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
                }

                List<BsonDocument> docs = obj.Select(x => x.ToBsonDocument()).ToList();
                await col.InsertManyAsync(docs);
                return new MongoResult<object>
                {
                    Success = true,
                    ErrorDesc = string.Empty,
                    Result = null
                };
            }
            catch (Exception ex) 
            {
                string str = $"[MongoInsert] execute insert failed, db:{dbName}, col:{collName}, ex:{ex}";
                Log.Error(str);
                return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
            }
        }

        public async Task<MongoResult<object>> Delete(string dbName, string collName, string filterKey, string filterValue)
        {
            try
            {
                var col = GetCollection(dbName, collName);
                if (col == null)
                {
                    string str = $"[Delete] cannot find collection, db:{dbName}, col:{collName}";
                    Log.Error(str);
                    return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
                }

                var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
                await col.DeleteManyAsync(filter);
                return new MongoResult<object>
                {
                    Success = true,
                    ErrorDesc = string.Empty,
                    Result = null
                };
            }
            catch(Exception ex) 
            {
                string str = $"[MongoDelete] execute delete failed, db:{dbName}, col:{collName}, ex:{ex}";
                Log.Error(str);
                return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
            }
        }

        public async Task<MongoResult<T>> Find<T>(string dbName, string collName, string filterKey, string filterValue) where T : class
        {
            try
            {
                var col = GetCollection(dbName, collName);
                if (col == null)
                {
                    string str = $"[Find] cannot find collection, db:{dbName}, col:{collName}";
                    Log.Error(str);
                    return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
                }

                var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
                var result = await col.Find(filter).ToListAsync();
                var res = result.Select(x => BsonSerializer.Deserialize<T>(x)).ToList();
                return new MongoResult<T>
                {
                    Success = true,
                    ErrorDesc = string.Empty,
                    Result = res
                };
            }
            catch (Exception ex)
            {
                string str = $"[MongoFind] execute find failed, db:{dbName}, col:{collName}, ex:{ex}";
                Log.Error(str);
                return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
            }
        }

        public async Task<MongoResult<T>> Save<T>(string dbName, string collName, string filterKey, string filterValue, T data) where T : class
        {
            try
            {
                var col = GetCollection(dbName, collName);
                if (col == null)
                {
                    string str = $"[Save] cannot find collection, db:{dbName}, col:{collName}";
                    Log.Error(str);
                    return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
                }

                var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
                var replacement = data.ToBsonDocument();
                var option = new FindOneAndReplaceOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, IsUpsert = true };
                var result = await col.FindOneAndReplaceAsync(filter, replacement, option);
                var res = new List<T>() { BsonSerializer.Deserialize<T>(result) };
                return new MongoResult<T>
                {
                    Success = true,
                    ErrorDesc = string.Empty,
                    Result = res
                };
            }
            catch (Exception ex)
            {
                string str = $"[MongoSave] execute save failed, db:{dbName}, col:{collName}, ex:{ex}";
                Log.Error(str);
                return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
            }
        }

        public async Task<MongoResult<object>> Update<T>(string dbName, string collName, string filterKey, string filterValue, string dataKey, T data) where T : class
        {
            try
            {
                var col = GetCollection(dbName, collName);
                if (col == null)
                {
                    string str = $"[Update] cannot find collection, db:{dbName}, col:{collName}";
                    Log.Error(str);
                    return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
                }

                var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
                var updator = Builders<BsonDocument>.Update.Set(dataKey, data.ToBsonDocument());
                var option = new UpdateOptions { IsUpsert = true };
                await col.UpdateOneAsync(filter, updator, option);
                return new MongoResult<object>
                {
                    Success = true,
                    ErrorDesc = string.Empty,
                    Result = null
                };
            }
            catch (Exception ex)
            {
                string str = $"[MongoUpdate] execute update failed, db:{dbName}, col:{collName}, ex:{ex}";
                Log.Error(str);
                return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
            }
        }

        private bool Setup()
        {
            try
            {
                string strSrv = string.IsNullOrEmpty(m_config.srv) ? "" : "+srv";
                string host = m_config.ip;
                ushort port = m_config.port;
                string user = m_config.user;
                string pwd = m_config.pwd;
                if (string.IsNullOrEmpty(m_config.user) || string.IsNullOrEmpty(m_config.pwd))
                {
                    m_connectionUrl = $"mongodb{strSrv}://{host}:{port}";
                }
                else
                {
                    m_connectionUrl = $"mongodb{strSrv}://{user}:{pwd}@{host}:{port}/?authMechanism=SCRAM-SHA-1";
                }

                m_client = new MongoClient(m_connectionUrl);
                foreach (var db in m_config.dbs)
                {
                    if (m_collections.ContainsKey(db.dbName))
                    {
                        Log.Error($"db name has exist:{db.dbName}");
                        return false;
                    }

                    m_collections[db.dbName] = new Dictionary<string, IMongoCollection<BsonDocument>>();
                    var collDic = m_collections[db.dbName];
                    foreach (var col in db.collections)
                    {
                        string colName = col.collectionName;
                        string[] indexList = col.indexNames;
                        if (collDic.ContainsKey(colName))
                        {
                            Log.Error($"collection name has exist, db:{db.dbName}, coll:{colName}");
                            return false;
                        }

                        var collection = m_client.GetDatabase(db.dbName).GetCollection<BsonDocument>(colName);
                        foreach (var name in indexList)
                        {
                            IndexKeysDefinition<BsonDocument> indexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending(name);
                            CreateIndexOptions option = new CreateIndexOptions{ Unique = true };
                            collection.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(indexKeysDefinition, option));
                        }

                        collDic[colName] = collection;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Connect to MongoDB failed:{e}");
                return false;
            }

            return true;
        }

        private IMongoCollection<BsonDocument> GetCollection(string dbName, string collName)
        {
            if (m_collections.TryGetValue(dbName, out var db))
            {
                if (db.TryGetValue(collName, out var coll))
                {
                    return coll;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }

}
