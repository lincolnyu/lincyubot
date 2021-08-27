using System;

namespace bot2.Logging
{
    class ConsoleLogger : ILogger
    {
        public void Log(string v)
        {
            Console.WriteLine(v);
        }
    }
}