using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;


namespace ZQ;

/*
    How to use:
        public struct UserData
        {
            public int id;
            public string name;
        }
        public static void TestWebApi(HttpListenerRequest req, HttpListenerResponse res)
        {
            UserData userData = new UserData
            {
                id = 100,
                name = "zq",
            };
            string data = JsonConvert.SerializeObject(userData);
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            res.OutputStream.Write(bytes);
            res.StatusCode = 200;
        }

       HttpServer httpServer = new HttpServer(urls);
        httpServer.RegisterRouter("/user/test", TestWebApi);
*/
public class HttpServer
{
    private bool m_running = false;
    private HttpListener m_listener;
    private Dictionary<string, Action<HttpListenerRequest, HttpListenerResponse>> m_callbacks = new();

    public List<string> m_url;

    public HttpServer(List<string> urls)
    {
        m_listener = new HttpListener();
        m_url = urls;
        foreach (var url in m_url)
        {
            m_listener.Prefixes.Add(url);
        }
    }

    public void RegisterRouter(string router, Action<HttpListenerRequest, HttpListenerResponse> callback)
    {
        if (m_callbacks.ContainsKey(router))
        {
            Log.Error($"RegisterRouter: router: {router} has registered.");
            return;
        }

        m_callbacks[router] = callback;
    }

    public bool Start()
    {
        try
        {
            StartListen();
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"HttpServer init failed, url:{m_url}, exception:{e}");
            return false;
        }
    }

    public void Shutdown()
    {
        m_running = false;
        m_listener.Stop();
    }

    private async void StartListen()
    {
        m_listener.Start();
        m_running = true;
        while (m_running)
        {
            try
            {
                HttpListenerContext ctx = await m_listener.GetContextAsync();
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse res = ctx.Response;

                string absolutePath = req.Url.AbsolutePath;
                if (m_callbacks.TryGetValue(absolutePath, out var callback))
                {
                    callback?.Invoke(req, res);
                }
                else
                {
                    res.StatusCode = 404;
                }

                res.ContentType = "application/json";
                res.ContentEncoding = Encoding.UTF8;
                res.Close();
            }
            catch (Exception e)
            {
                Log.Error($"HttpServer listen error, url:{m_url}, exception:{e}");
            }
        }
    }
}

