using System.Collections.Concurrent;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;


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
        internal class MongoResponse
        {
            public bool success;
            public string errorDesc;
            public List<BsonDocument> result;
        }

        internal class MongoTask
        {
            public IMongoCollection<BsonDocument> collection;
            public TaskCompletionSource<MongoResponse> resultTcs;
        }

        internal class MongoTaskInsert : MongoTask
        {
            public List<BsonDocument> docs;
            public MongoTaskInsert(IMongoCollection<BsonDocument> col, TaskCompletionSource<MongoResponse> resultTcs, List<BsonDocument> docs)
            {
                this.collection = col;
                this.docs = docs;
                this.resultTcs = resultTcs;
            }
        }

        internal class MongoTaskDelete : MongoTask
        {
            public FilterDefinition<BsonDocument> filter;
            public MongoTaskDelete(IMongoCollection<BsonDocument> col, TaskCompletionSource<MongoResponse> resultTcs, FilterDefinition<BsonDocument> filter)
            {
                this.collection = col;
                this.filter = filter;
                this.resultTcs = resultTcs;
            }
        }

        internal class MongoTaskFind : MongoTask
        {
            public FilterDefinition<BsonDocument> filter;
            public MongoTaskFind(IMongoCollection<BsonDocument> col, TaskCompletionSource<MongoResponse> resultTcs, FilterDefinition<BsonDocument> filter)
            {
                this.collection = col;
                this.filter = filter;
                this.resultTcs = resultTcs;
            }
        }

        internal class MongoTaskSave : MongoTask
        {
            public FilterDefinition<BsonDocument> filter;
            public BsonDocument replacement;
            public FindOneAndReplaceOptions<BsonDocument> option;
            public MongoTaskSave(IMongoCollection<BsonDocument> col, TaskCompletionSource<MongoResponse> resultTcs, 
                FilterDefinition<BsonDocument> filter, BsonDocument replacement, FindOneAndReplaceOptions<BsonDocument> option)
            {
                this.collection = col;
                this.filter = filter;
                this.resultTcs = resultTcs;
                this.option = option;
                this.replacement = replacement;
            }
        }

        internal class MongoTaskUpdate : MongoTask
        {
            public FilterDefinition<BsonDocument> filter;
            public UpdateDefinition<BsonDocument> updator;
            public UpdateOptions option;
            public MongoTaskUpdate(IMongoCollection<BsonDocument> col, TaskCompletionSource<MongoResponse> resultTcs,
                FilterDefinition<BsonDocument> filter, UpdateDefinition<BsonDocument> updator, UpdateOptions option)
            {
                this.collection = col;
                this.filter = filter;
                this.resultTcs = resultTcs;
                this.option = option;
                this.updator = updator;
            }
        }

        internal class MongoTaskCallback
        {
            public readonly TaskCompletionSource completeTcs;
            public readonly TaskCompletionSource<MongoResponse> resultTcs;
            public MongoTaskCallback(TaskCompletionSource completeTcs, TaskCompletionSource<MongoResponse> resultTcs)
            {
                this.completeTcs = completeTcs;
                this.resultTcs = resultTcs;
            }
        }

        private string m_connectionUrl;
        private MongoDBSetupConfig m_config;
        private MongoClient m_client;

        private Dictionary<string, Dictionary<string, IMongoCollection<BsonDocument>>> m_collections = new();

        private Thread m_thread;

        private ConcurrentQueue<MongoTask> m_queueTasks = new ConcurrentQueue<MongoTask>();
        private Queue<MongoTaskCallback> m_queueCallbacks = new Queue<MongoTaskCallback>();

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

            m_thread = new Thread(TaskThread);
            m_thread.Start();

            return true;
        }

        public bool Update(long timeNow)
        {
            processCallbacks();
            return true;
        }

        public bool Shutdown()
        {
            return true;
        }

        public async Task<MongoResult<object>> Insert<T>(string dbName, string collName, List<T> obj) where T : class
        {
            var col = GetCollection(dbName, collName);
            if (col == null)
            {
                string str = $"[Insert] cannot find collection, db:{dbName}, col:{collName}";
                Log.Error(str);
                return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null};
            }

            List<BsonDocument> docs = obj.Select(x=> x.ToBsonDocument()).ToList();

            var completeTcs = new TaskCompletionSource();
            var resultTcs = new TaskCompletionSource<MongoResponse>();

            MongoTaskInsert task = new MongoTaskInsert(col, resultTcs, docs);
            m_queueTasks.Enqueue(task);

            MongoTaskCallback cb = new MongoTaskCallback(completeTcs, resultTcs);
            m_queueCallbacks.Enqueue(cb);

            await completeTcs.Task;

            MongoResponse res = await resultTcs.Task;
            return new MongoResult<object>
            {
                Success = true,
                ErrorDesc = string.Empty,
                Result = null
            };
        }

        public async Task<MongoResult<object>> Delete(string dbName, string collName, string filterKey, string filterValue)
        {
            var col = GetCollection(dbName, collName);
            if (col == null)
            {
                string str = $"[Delete] cannot find collection, db:{dbName}, col:{collName}";
                Log.Error(str);
                return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
            }

            var completeTcs = new TaskCompletionSource();
            var resultTcs = new TaskCompletionSource<MongoResponse>();

            var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
            MongoTaskDelete task = new MongoTaskDelete(col, resultTcs, filter);
            m_queueTasks.Enqueue(task);

            MongoTaskCallback cb = new MongoTaskCallback(completeTcs, resultTcs);
            m_queueCallbacks.Enqueue(cb);

            await completeTcs.Task;
            MongoResponse res = await resultTcs.Task;
            return new MongoResult<object>
            {
                Success = res.success,
                ErrorDesc = res.errorDesc,
                Result = null
            };
        }

        public async Task<MongoResult<T>> Find<T>(string dbName, string collName, string filterKey, string filterValue) where T : class
        {
            var col = GetCollection(dbName, collName);
            if (col == null)
            {
                string str = $"[Find] cannot find collection, db:{dbName}, col:{collName}";
                Log.Error(str);
                return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
            }

            var completeTcs = new TaskCompletionSource();
            var resultTcs = new TaskCompletionSource<MongoResponse>();

            var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
            var task = new MongoTaskFind(col, resultTcs, filter);
            m_queueTasks.Enqueue(task);

            MongoTaskCallback cb = new MongoTaskCallback(completeTcs, resultTcs);
            m_queueCallbacks.Enqueue(cb);

            await completeTcs.Task;

            MongoResponse res = await resultTcs.Task;

            try
            {
                var result = res.result.Select(x => BsonSerializer.Deserialize<T>(x)).ToList();
                return new MongoResult<T>
                {
                    Success = res.success,
                    ErrorDesc = res.errorDesc,
                    Result = result
                };
            }
            catch (Exception ex) 
            {
                string str = $"[Find] convert bson to object failed db:{dbName}, col:{collName}, ex:{ex}";
                Log.Error(str);
                return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
            }
        }

        public async Task<MongoResult<T>> Save<T>(string dbName, string collName, string filterKey, string filterValue, T data) where T : class
        {
            var col = GetCollection(dbName, collName);
            if (col == null)
            {
                string str = $"[Save] cannot find collection, db:{dbName}, col:{collName}";
                Log.Error(str);
                return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
            }

            var completeTcs = new TaskCompletionSource();
            var resultTcs = new TaskCompletionSource<MongoResponse>();

            var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
            var replacement = data.ToBsonDocument();
            var option = new FindOneAndReplaceOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, IsUpsert = true };
            MongoTaskSave task = new MongoTaskSave(col, resultTcs, filter, replacement, option);
            m_queueTasks.Enqueue(task);

            MongoTaskCallback cb = new MongoTaskCallback(completeTcs, resultTcs);
            m_queueCallbacks.Enqueue(cb);

            await completeTcs.Task;

            MongoResponse res = await resultTcs.Task;

            try
            {
                var result = res.result.Select(x => BsonSerializer.Deserialize<T>(x)).ToList();
                return new MongoResult<T>
                {
                    Success = res.success,
                    ErrorDesc = res.errorDesc,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                string str = $"[Save] convert bson to object failed db:{dbName}, col:{collName}, ex:{ex}";
                Log.Error(str);
                return new MongoResult<T> { ErrorDesc = str, Success = false, Result = null };
            }
        }

        public async Task<MongoResult<object>> Update<T>(string dbName, string collName, string filterKey, string filterValue, string dataKey, T data) where T : class
        {
            var col = GetCollection(dbName, collName);
            if (col == null)
            {
                string str = $"[Update] cannot find collection, db:{dbName}, col:{collName}";
                Log.Error(str);
                return new MongoResult<object> { ErrorDesc = str, Success = false, Result = null };
            }

            var completeTcs = new TaskCompletionSource();
            var resultTcs = new TaskCompletionSource<MongoResponse>();

            var filter = Builders<BsonDocument>.Filter.Eq(filterKey, filterValue);
            var updator = Builders<BsonDocument>.Update.Set(dataKey,  data.ToBsonDocument());
            var option = new UpdateOptions { IsUpsert = true };
            MongoTaskUpdate task = new MongoTaskUpdate(col, resultTcs, filter, updator, option);
            m_queueTasks.Enqueue(task);

            MongoTaskCallback cb = new MongoTaskCallback(completeTcs, resultTcs);
            m_queueCallbacks.Enqueue(cb);

            await completeTcs.Task;

            MongoResponse res = await resultTcs.Task;
            return new MongoResult<object>
            {
                Success = res.success,
                ErrorDesc = res.errorDesc,
                Result = null
            };
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

        private void processCallbacks()
        {
            if (m_queueCallbacks.Count == 0 || !m_queueCallbacks.TryPeek(out var callback))
            {
                return;
            }

            if (!callback.resultTcs.Task.IsCompleted)
            {
                return;
            }

            m_queueCallbacks.Dequeue();
            callback.completeTcs.SetResult();
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

        private void TaskThread()
        {
            while (true)
            {
                if (m_queueTasks.Count == 0 || !m_queueTasks.TryDequeue(out var task))
                {
                    continue;
                }

                var t = ExecuteTask(task);
                t.Wait();
            }
        }

        private async Task ExecuteTask(MongoTask task)
        {
            try
            {
                if (task is MongoTaskInsert taskInsert)
                {
                    await MongoInsert(taskInsert);
                }
                else if (task is MongoTaskDelete taskDelete)
                {
                    await MongoDelete(taskDelete);
                }
                else if (task is MongoTaskFind taskFind)
                {
                    await MongoFind(taskFind);
                }
                else if (task is MongoTaskSave taskSave)
                {
                    await MongoSave(taskSave);
                }
                else if (task is MongoTaskUpdate taskUpdate)
                {
                    await MongoUpdate(taskUpdate);
                }
                else
                {
                    string str = $"not support mongo task type:{task.GetType()}";
                    Log.Error(str);
                    task.resultTcs.SetResult(new MongoResponse { success = false, errorDesc = str, result = null });
                }
            }
            catch (Exception e)
            {
                string str = $"execute mongo command failed, type:{task.GetType()}, ex:{e}";
                Log.Error(str);
                task.resultTcs.SetResult(new MongoResponse { success = false, errorDesc = str, result = null});
            }
        }

        private async Task MongoInsert(MongoTaskInsert task) 
        {
            var collection = task.collection;
            await collection.InsertManyAsync(task.docs);
            task.resultTcs.SetResult(new MongoResponse { success = true, errorDesc = string.Empty});
        }

        private async Task MongoDelete(MongoTaskDelete task)
        {
            var collection = task.collection;
            await collection.DeleteManyAsync(task.filter);
            task.resultTcs.SetResult(new MongoResponse { success = true, errorDesc = string.Empty });
        }

        private async Task MongoFind(MongoTaskFind task)
        {
            var collection = task.collection;
            var result = await collection.Find(task.filter).ToListAsync();
            task.resultTcs.SetResult(new MongoResponse { success = true, errorDesc = string.Empty, result = result });
        }

        private async Task MongoSave(MongoTaskSave task)
        {
            var collection = task.collection;
            var result = await collection.FindOneAndReplaceAsync(task.filter, task.replacement, task.option);
            task.resultTcs.SetResult(new MongoResponse { success = true, errorDesc = string.Empty, result = new List<BsonDocument> { result } });
        }

        private async Task MongoUpdate(MongoTaskUpdate task)
        {
            var collection = task.collection;
            await collection.UpdateOneAsync(task.filter, task.updator, task.option);
            task.resultTcs.SetResult(new MongoResponse { success = true, errorDesc = string.Empty});
        }
    }

}
