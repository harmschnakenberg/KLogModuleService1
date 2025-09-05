using System.Data;
using System.Runtime.CompilerServices;

namespace KLogModuleService1
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;

        internal static Mutex MyMutex = new(false);

        public static void LogInfo(string message)
        {
            Console.WriteLine($"{DateTime.Now.ToShortTimeString()}\t{message}");       
        }

        public static void LogWarning(string message)
        {
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine($"{DateTime.Now.ToShortTimeString()}\t{message}");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void LogError(string message)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine($"{DateTime.Now.ToShortTimeString()}\t{message}");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            WebServer.Start();

            MyS7.AddCpu(Sql.GetAllPlc());
            //var Sekunde = new S7.Net.Types.DataItem() { DataType = S7.Net.DataType.DataBlock, DB = 10, StartByteAdr = 6, VarType = S7.Net.VarType.Word };
            //var Minute = new S7.Net.Types.DataItem() { DataType = S7.Net.DataType.DataBlock, DB = 10, StartByteAdr = 4, VarType = S7.Net.VarType.Word };
            //var Stunde = new S7.Net.Types.DataItem() { DataType = S7.Net.DataType.DataBlock, DB = 10, StartByteAdr = 2, VarType = S7.Net.VarType.Word };

            //MyS7.TagList.Add("A01", [Stunde, Minute, Sekunde]);

            DataTable dt = Sql.DataSelectDataTable("SELECT TagName FROM TagNames; ", null); // TEST Datenbank erstellen

            if (dt.Rows.Count == 0)
                Worker.LogWarning("Es konnten keine Variablen zum Auslesen aus der Datenbank geladen werden.");

            MyS7.ReadTags(dt);



            while (!stoppingToken.IsCancellationRequested)
            {
                MyMutex.WaitOne();
                MyS7.ReadAllTags(stoppingToken);

//                if (_logger.IsEnabled(LogLevel.Information))
//                {
//#if DEBUG
//                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now.TimeOfDay);
//#endif
//                }
                MyMutex.ReleaseMutex();
                await Task.Delay(3000, stoppingToken);
            }

            WebServer.Stop();
        }
    }
}
