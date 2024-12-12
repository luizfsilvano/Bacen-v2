using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;

namespace Bacen_v2.Utils
{
    public static class Logger
    {
        private static string LogFile = "Data/logs/app.log";

        public static void Init()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, $"=== Log iniciado: {DateTime.Now} ===\n");
        }

        public static void Log(string message)
        {
            var logEntry = $"{DateTime.Now}: {message}\n";
            File.AppendAllText(LogFile, logEntry);
            Console.WriteLine(message);
        }
    }
}