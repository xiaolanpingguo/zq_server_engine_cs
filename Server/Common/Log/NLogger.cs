using System;
using System.Xml.Linq;
using NLog;

namespace ZQ
{
    public class NLogger: ILog
    {
        private readonly NLog.Logger m_logger;

        public NLogger(string serverId, string configPath)
        {
            LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(configPath);
            LogManager.Configuration.Variables["serverId"] = serverId;
            m_logger = LogManager.GetLogger("Server");
        }

        public void Debug(string message)
        {
            m_logger.Debug(message);
        }

        public void Info(string message)
        {
            m_logger.Info(message);
        }

        public void Warning(string message)
        {
            m_logger.Warn(message);
        }

        public void Error(string message)
        {
            m_logger.Error(message);
        }
    }
}