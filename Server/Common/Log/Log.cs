using NLog;
using System;
using System.Diagnostics;
using System.Globalization;

namespace ZQ
{
    public class Log
    {
        public enum LogType
        {
            Debug,
            Info,
            Warning,
            Error
        }

        private ILog m_log;
        private bool m_enableConsole = true;
        private LogType m_level = LogType.Debug;
        private static Log m_instance = null;

        public static Log Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new Log();
                }
                return m_instance;
            }
        }

        public bool Init(string serverId, string fileConfig, LogType level = LogType.Debug, bool enableConsole = true)
        {
            m_log = new NLogger(serverId, fileConfig);
            m_level = level;
            m_enableConsole = enableConsole;
            return true;
        }

        public static void Debug(string msg)
        {
            Instance.LogMessage(LogType.Debug, msg);
        }

        public static void Info(string msg)
        {
            Instance.LogMessage(LogType.Info, msg);
        }

        public static void Warning(string msg)
        {
            Instance.LogMessage(LogType.Warning, msg);
        }

        public static void Error(string msg)
        {
            Instance.LogMessage(LogType.Error, msg);
        }

        private bool CheckLogLevel(LogType level)
        {
            return m_level <= level;
        }

        private void LogMessage(LogType type, string msg)
        {
            if (m_log == null)
            {
                return;
            }

            if (!CheckLogLevel(type))
            {
                return;
            }

            if (m_enableConsole)
            {
                var now = DateTime.Now;
                string str = $"[{now}] [{type.ToString()}]: {msg}";

                if (type >= LogType.Error)
                {
                    StackTrace st = new StackTrace(2, true);
                    System.Console.WriteLine($"{str}\n ---- StackTrace Begin ----\n{st.ToString()}{"---- StackTrace End ----"}");
                }
                else
                {
                    System.Console.WriteLine(str);
                }
            }

            if (type == LogType.Debug)
            {
                m_log.Debug(msg);
            }
            else if (type == LogType.Info)
            {
                m_log.Info(msg);
            }
            else if (type == LogType.Warning)
            {
                m_log.Warning(msg);
            }
            else if (type == LogType.Error)
            {
                StackTrace st = new StackTrace(2, true);
                m_log.Error($"{msg}\n StackTrace:{st.ToString()}");
            }
        }
    }

}