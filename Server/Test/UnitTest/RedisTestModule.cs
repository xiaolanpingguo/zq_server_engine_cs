using ZQ.Redis;


namespace ZQ;


public class RedisTestModule : IModule
{
    private RedisModule m_redis;

    public RedisTestModule()
    {
        RedisConfig config = new RedisConfig()
        {
            ip = "127.0.0.1",
            port = 6379,
            pwd = "",
        };

        m_redis = new RedisModule(config.ip, config.port, config.pwd);
    }

    public bool Init()
    {
        m_redis.Init();
        return true;
    }

    public bool Update(long timeNow)
    {
        m_redis.Update(timeNow);
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
            TestSet();
        }
    }

    private async void TestSet()
    {
        string key = "zq";
        string value = "123456";
        Console.WriteLine($"RedisTestModule: will test: TestSet");
        bool success = await m_redis.SET(key, value);
        if (!success)
        {
            Console.WriteLine($"RedisTestModule, TestSet failed");
            return;
        }
        else
        {
            Console.WriteLine($"RedisTestModule: TestSet success.");
        }
    }
}
