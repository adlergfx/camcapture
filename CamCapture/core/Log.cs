using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace CamCapture.core
{
    class Log
    {
        private static Log Instance;
        private LogLevel logLevel = LogLevel.Info;
        private string ? logFile;

        public delegate void LogHandler(LogLevel level, string message);
        public event LogHandler OnLogEvent = delegate { };

        public enum LogLevel
        {
            Info,
            Debug,
            Error
        }

        private Log(string ? filename)
        {
            if (filename != null) logFile = filename;
        }

        public static Log Create(string ? filename = null)
        {
            Instance = new Log(filename);
            return Instance;
        }

        public static Log Get()
        {
            if (Instance == null) return Create();
            return Instance;
        }

        private void SetLogLevel(LogLevel level)
        {
            logLevel = level;
        }

        private Log Write(LogLevel lvl, string message)
        {
            if (lvl < logLevel) return this;
            OnLogEvent(lvl, message);
            string msg = $"{lvl.ToString()}: {message}";

            System.Diagnostics.Debug.WriteLine(msg);
            

            

            if (logFile != null)
            {
                msg = $"{DateTime.Now} - {msg}";
                string[] arr = new string[1] { msg };
                File.AppendAllLines(logFile, arr);
            }

            return this;
        }

        public static Log Debug(string message) => Get().Write(LogLevel.Debug, message);
        public static Log Error(string message) => Get().Write(LogLevel.Error, message);
        public static Log Info(string message) => Get().Write(LogLevel.Info, message);
    }
}
