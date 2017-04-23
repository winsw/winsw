using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace winsw.Logging
{
    /// <summary>
    /// Log Appender for executable STDERR/STDOUT, which passes all logging to log4net.
    /// </summary>
    public abstract class Log4NetLogHandlerBase : LogHandler
    {
        public bool IndependentErrLog { get; private set; }
        
        public ILog Out { get; private set; }
        public ILog Err { get; private set; }

        public Log4NetLogHandlerBase(bool independentErrLog)
        {
            IndependentErrLog = independentErrLog;
            if (independentErrLog)
            {
                Out = LogManager.GetLogger("WinSW.stdout");
                Err = LogManager.GetLogger("WinSW.stderr");
            }
            else
            {
                Err = Out = LogManager.GetLogger("WinSW.out");
            }
        }

        public override void log(Stream outputStream, Stream errorStream)
        {
            new Thread(delegate() { CopyStreamToLogger(outputStream, Out, false); }).Start();
            new Thread(delegate() { CopyStreamToLogger(errorStream, Err, true); }).Start();
        }

        /// <summary>
        /// Convenience method to copy stuff from StreamReader to StreamWriter
        /// </summary>
        protected static void CopyStreamToLogger(Stream input, ILog output, bool isErrorLog)
        {
            StreamReader stream = new StreamReader(input);

            StringBuilder sb = new StringBuilder();
            int symbol = stream.Peek();
            while (symbol != -1)
            {
                symbol = stream.Read();
                if (symbol == 13 && stream.Peek() == 10) // \r\n
                {
                    stream.Read();
                    var line = sb.ToString();
                    // TODO: any better way to do that without accessing logger?
                    if (isErrorLog) 
                    {
                        output.Error(line);
                    } 
                    else 
                    {
                        output.Info(line);
                    }
                    
                    // TODO: replace by clear() after adopting newer framework version
                    sb = new StringBuilder();
                }
                else
                {
                    sb.Append((char)symbol);
                }
            }
        }
    }

    /// <summary>
    /// Log handler, which redirects process STDOUT/STDERR to a log4net engine.
    /// </summary>
    public class ProcessOnlyLog4NetHandler : Log4NetLogHandlerBase
    {
        public ProcessOnlyLog4NetHandler(bool independentErrLog) :
            base(independentErrLog)
        {  }
    }

    /// <summary>
    /// Log handler, which indicates the entire logging subsystem is configured by a config file.
    /// </summary>
    public class ConfigDefinedLog4NetHandler : Log4NetLogHandlerBase
    {
        public string ConfigPath { get; private set; }

        public ConfigDefinedLog4NetHandler(bool independentErrLog, string confgPath) :
            base(independentErrLog)
        {
            ConfigPath = confgPath;
        }
    }
}
