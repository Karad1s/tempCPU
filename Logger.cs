using System;
using System.IO;

namespace tempCPU
{
    public static class Logger
    {
        private static readonly string LogDirectory;
        private static readonly string LogFilePath;
        private static readonly object LockObj = new();

        static Logger()
        {
            // Берём безопасную директорию: %LocalAppData%\CpuTempTrayWpf\logs
            LogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CpuTempTrayWpf",
                "logs"
            );

            // Создаём, если нет
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);

            LogFilePath = Path.Combine(LogDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
        }

        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warning(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message) => Log(LogLevel.Error, message);

        private static void Log(LogLevel level, string message)
        {
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string levelString = level.ToString().ToUpper();
            string logLine = $"[{timeStamp}] [{levelString}] {message}";

            lock (LockObj)
            {
                try
                {
                    File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
                }
                catch
                {
                    // Если файл занят или недоступен — молча игнорируем
                }
            }

            // Для дебага в консоли — разными цветами
            var originalColor = Console.ForegroundColor;
            switch (level)
            {
                case LogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }

            Console.WriteLine(logLine);
            Console.ForegroundColor = originalColor;
        }
    }
}
