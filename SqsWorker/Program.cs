using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using CommandLine.Text;

namespace SqsWorker
{
    class Program
    {
        /// <summary>
        /// The NLog logger.
        /// </summary>
        private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">The parsable arguments into <see cref="Options"/>.</param>
        /// <returns>Errorcode. Success is <c>0</c>; otherwise, <c>1</c>.</returns>
        static int Main(string[] args)
        {
            try
            {
                var options = new Options();
                if (CommandLine.Parser.Default.ParseArguments(args, options))
                {
                    using (var queue = new SqsWorkQueue(options))
                    {
                        queue.Work();
                    }
                }
                else
                {                    
                    Console.WriteLine(HelpText.AutoBuild(options));
                }
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Fatal("An unhandled exception occured during execution. Exception: {0}", ex.ToString());
                return 1;
            }
        }
    }
}
