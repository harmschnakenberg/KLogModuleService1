using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KLogModuleService1
{
    internal partial class Sql
    {
        private static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        static string MasterDbPath { get; } = Path.Combine(AppFolder, "db", "KLogMaster.db");
        static string DailyDbPath { get; } = Path.Combine(AppFolder, "db", DateTime.Now.ToString("yyyyMMdd") + ".db");
         

        internal static bool CheckDbFile(string dbPath, bool testOpen = true)
        {

#if DEBUG
            //File.Delete(dbPath); //TEST

            //Console.WriteLine(dbPath);
#endif


            if (!File.Exists(dbPath))
            {
                if (dbPath == MasterDbPath)
                    CreateDataBaseMaster();
                if (dbPath == DailyDbPath)
                    CreateDataBaseDaily();
            }

            #region Mehrfach versuchen die Datenbank zu öffnen, falls sie grade in Benutzung ist

            int numTries = 10;

            while (numTries > 0)
            {
                --numTries;

                try
                {
                    using FileStream stream = new FileInfo(MasterDbPath).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stream.Close();
                    break;
                }
                catch (IOException)
                {
                    //the file is unavailable because it is:
                    //still being written to
                    //or being processed by another thread
                    //or does not exist (has already been processed)
                    Thread.Sleep(200);
                }
            }

            if (numTries == 0)
            {
                string txt = $"Die Datenbankdatei >{MasterDbPath}< ist durch ein anderes Programm gesperrt.";
                Console.WriteLine(txt);
                //Sql.InsertLog(1, txt);
                Worker.LogWarning(txt);
            }
            return numTries > 0;

            #endregion
        }

        /// <summary>
        /// Erzeugt eine Datenbank für Stammdaten
        /// </summary>
        private static void CreateDataBaseMaster()
        {
            Worker.LogInfo($"Erstelle eine neue Datenbank-Datei unter '{MasterDbPath}'");

            try
            {
                //Erstelle Datenbank-Datei und öffne einmal zum Testen
                string dir = Path.GetDirectoryName(MasterDbPath) ?? string.Empty;
                _ = Directory.CreateDirectory(dir);
                FileStream stream = File.Create(MasterDbPath);
                stream.Close();

                System.Threading.Thread.Sleep(500);

                #region Tabellen erstellen
                string query = @"CREATE TABLE IF NOT EXISTS Log ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, 
                          Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                          Category TEXT NOT NULL,
                          Content TEXT 
                          ); ";
                _ = NonQueryAsync(MasterDbPath, query, null);

                query = @"CREATE TABLE IF NOT EXISTS User ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,
                          IsAdmin INTEGER DEFAULT 0,
                          Password TEXT,
                          Identification TEXT
                          ); ";
                _ = NonQueryAsync(MasterDbPath, query, null);


                query = @"CREATE TABLE IF NOT EXISTS ConnectionType ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE                          
                          ); ";
                _ = NonQueryAsync(MasterDbPath, query, null);

                query = @"CREATE TABLE IF NOT EXISTS Source ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,
                          ConnectionType INTEGR NOT NULL DEFAULT 1,       
                          CpuType INTEGR NOT NULL DEFAULT 40,  
                          Ip TEXT, 
                          Port INTEGER DEFAULT 102,                           
                          Rack INTEGER DEFAULT 0,
                          Slot INTEGER DEFAULT 0,
                          Comment TEXT,

                          CONSTRAINT fk_ConnectionType FOREIGN KEY (ConnectionType) REFERENCES ConnectionType (Id) ON DELETE NO ACTION
                          ); "; //CONSTRAINT für CpuType?
                _ = NonQueryAsync(MasterDbPath, query, null);



                #endregion

                #region Tabellen füllen

                query = @"INSERT INTO Log (Category, Content) VALUES ('System', 'Datenbank neu erstellt.'); ";
                NonQueryAsync(MasterDbPath, query, null);

                query = $"INSERT INTO User (Name, IsAdmin, Password) VALUES ('admin', 1, '{Encrypt("admin")}'); ";
                NonQueryAsync(MasterDbPath, query, null);

                query = @"INSERT INTO ConnectionType (Name) VALUES ('S7'); ";
                NonQueryAsync(MasterDbPath, query, null);

                query = @"INSERT INTO Source (Name, Ip) VALUES ('A01', '192.168.0.10'); ";
                NonQueryAsync(MasterDbPath, query, null);

                #endregion

            }
            catch (Exception ex)
            {
                Worker.LogError("CreateNewDataBase() " + ex.Message + ex.StackTrace);
                throw;
            }

        }

        /// <summary>
        /// Erzeugt für jeden Tag eine Datenbank für die auzuzeichnenden Daten
        /// </summary>
        internal static void CreateDataBaseDaily()
        {

            Worker.LogInfo($"Erstelle eine neue Datenbank-Datei unter '{DailyDbPath}'");

            try
            {
                //Erstelle Datenbank-Datei und öffne einmal zum Testen
                string dir = Path.GetDirectoryName(DailyDbPath) ?? string.Empty;
                _ = Directory.CreateDirectory(dir);
                FileStream stream = File.Create(DailyDbPath);
                stream.Close();

                System.Threading.Thread.Sleep(500);

                #region Tabellen erstellen
                string query = @"CREATE TABLE IF NOT EXISTS TagNames (                                                   
                          TagName TEXT NOT NULL PRIMARY KEY,
                          TagType TEXT,
                          TagComment TEXT
                          ); ";
                _ = DataNonQueryAsync(query);


                query = @"CREATE TABLE IF NOT EXISTS Data (                         
                          Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                          TagName TEXT NOT NULL,
                          TagValue NUMERIC
                          ); ";
                _ = DataNonQueryAsync(query);

                #endregion
            }

            catch
            {
                throw new NotImplementedException();
            }
        }
    
    }
}
