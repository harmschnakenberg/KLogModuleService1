using Grapevine;
using S7.Net;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;

namespace KLogModuleService1
{
    /// <summary>
    /// Stammdatenmanipulation
    /// </summary>
    internal partial class Sql
    {

        public enum CpuConnectionType
        {
            S7 = 1
        }

        /// <summary>
        /// Master-Datenbank mit Stammdaten
        /// </summary>
        /// <param name="query"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static bool MasterNonQueryAsync(string query, Dictionary<string, object>? args)
        {
            return NonQueryAsync(MasterDbPath, query, args);
        }

        internal static bool DataNonQueryAsync(string query)
        {
            return NonQueryAsync(DailyDbPath, query, null);
        }

        #region Benutzerverwaltung
        internal static DataTable GetAllUsers()
        {
#if DEBUG
            string query = @"SELECT * FROM User; ";
            return Sql.SelectDataTable(MasterDbPath, query, null) ?? new System.Data.DataTable();
          
#else
            string query = @"SELECT Id, Name, IsAdmin FROM User; ";
            return Sql.SelectDataTable(MasterDbPath, query, null) ?? new System.Data.DataTable();
            //return Html.DataTableToHtml(dt);
#endif

        }


        private static User LogIn(IHttpContext context)
        {
            User user = new();

            if (!context.Locals.ContainsKey("FormData"))
                return user;

            var formContent = (Dictionary<string, string>?)context.Locals["FormData"];

            if (formContent is null
                || !formContent.TryGetValue("user", out string? username)
                || !formContent.TryGetValue("pswd", out string? password)
                )
                return user;

            username = WebUtility.UrlDecode(username ?? string.Empty).Replace("<", "&lt;").Replace(">", "&gt;");
            password = WebUtility.UrlDecode(password ?? string.Empty).Replace("<", "&lt;").Replace(">", "&gt;");

            //string username = WebUtility.UrlDecode(formContent["user"] ?? string.Empty).Replace("<", "&lt;").Replace(">", "&gt;"); //HTML unschädlich machen
            //string password = WebUtility.UrlDecode(formContent["pswd"] ?? string.Empty).Replace("<", "&lt;").Replace(">", "&gt;");

            string query = @"SELECT Id, Name, Identification, IsAdmin FROM User WHERE Name = @Name AND Password = @Password; ";
            Dictionary<string, object> args = new() { { "@Name", username }, { "@Password", Encrypt(password) } };

            DataTable dt = Sql.SelectDataTable(MasterDbPath, query, args);
            user = new User(dt);

            if (user.Name.Length > 0)
            {
                user.Auth = Guid.NewGuid().ToString();
                args = new Dictionary<string, object> { { "@Name", username }, { "@auth", user.Auth } };
                bool success = Sql.NonQueryAsync(MasterDbPath, @"UPDATE User SET Identification = @auth WHERE Name = @Name", args);

                if (!success)
                    return new User();
            }
            else
            {
                Worker.LogWarning("LogIn() Es konnte kein Benutzer angemeldet werden.");
            }

            return user;
        }


        internal static User LogedInUser(IHttpContext context)
        {
            User user = new();

            try
            {

                #region Sind Login-Formulardaten vorhanden?

                user = Sql.LogIn(context);

                if (user?.Name.Length > 0)
                    return user;

                #endregion

                #region Cookie auswerten

                string? auth = context.Request.Cookies.FirstOrDefault(c => c.Name == "auth")?.Value;

                if (auth is not null && auth.Length > 0)
                {
#if DEBUG
                    Worker.LogInfo($"Cookie gefunden {auth}");
#endif
                    string query = @"SELECT Id, Name, Identification, IsAdmin FROM User WHERE Identification = @auth";
                    Dictionary<string, object> args = new() { { "@auth", auth } };

                    DataTable dt = SelectDataTable(MasterDbPath, query, args);
                    user = new User(dt);
                }

                #endregion
            }
            catch (Exception ex)
            {
                Worker.LogError("Fehler  LogedInUserAsync() " + ex);
            }

            user ??= new User();

            return user;
        }


        private static string Encrypt(string password)
        {
            if (password == null) return string.Empty;

            byte[] data = System.Text.Encoding.UTF8.GetBytes(password);

            ///TODO: Salting oder andere Sicherheitsverbesserungen nachpflegen

            data = System.Security.Cryptography.SHA256.HashData(data);
#if DEBUG
            Console.WriteLine($"Passwort '{password}' -> '{System.Text.Encoding.UTF8.GetString(data)}'");
#endif
            return System.Text.Encoding.UTF8.GetString(data);
        }


        internal static bool CreateOrUpdateUser(Dictionary<string, string> form, User admin)
        {
#if DEBUG
            Worker.LogWarning(string.Join(' ', form));
#endif

            if (admin.Name.Length < 2)
                return false;

            #region Formular auslesen
            string userid = WebUtility.UrlDecode(form["userid"]);
            string username = WebUtility.UrlDecode(form["username"]);
            string userpswd = WebUtility.UrlDecode(form["userpswd"]) ?? string.Empty;
            string isadmin = WebUtility.UrlDecode(form["isadmin"]) ?? "0";

            bool success = false;
            #endregion

            #region Benutzer erstellen oder ändern und ggf. Passwort neu setzen
            if (!string.IsNullOrWhiteSpace(username))
            {
                Dictionary<string, object> args = new()
                {
                    { "@Id", userid },
                    { "@Name", username },
                    { "@Password", string.IsNullOrWhiteSpace(userpswd) ? string.Empty : Encrypt(userpswd) },
                    { "@IsAdmin", isadmin ?? "0" },
                    { "@AdminId", admin.Id }
                };

                string query = @" 
                INSERT INTO User ( Name, IsAdmin, Password ) VALUES (
                @Name, @IsAdmin, @Password )
                ON CONFLICT(Name) DO UPDATE SET
                    Name = @Name,
                    IsAdmin = @IsAdmin
                WHERE (SELECT IsAdmin FROM User WHERE Id = @AdminId) == 1;
                ;   
                UPDATE OR IGNORE User SET                     
                Password = @Password
                WHERE Id = @Id 
                AND @Password IS NOT ''; ";

                success = Sql.MasterNonQueryAsync(query, args);
            }

            #endregion

            if (success)
                Worker.LogInfo($"Benutzer {username} wurde durch {admin.Name} geändert.");

            return success;
        }

        internal static bool DeleteUser(Dictionary<string, string> form, User admin)
        {
#if DEBUG
            Worker.LogWarning(string.Join(' ', form));
#endif
            try
            {
                string userid = WebUtility.UrlDecode(form["userid"]);

                if (string.IsNullOrWhiteSpace(userid))
                    return false;

                var args = new Dictionary<string, object>
                {
                    { "@Id", userid },
                    { "@AdminId", admin.Id },
                };

                //Der angemeldete Benutzer muss ein Administrator sein und kann sich nicht selbst löschen.                
                string query = @"
                    DELETE FROM User 
                    WHERE Id = @Id 
                    AND Id != @AdminId
                    AND (SELECT IsAdmin FROM User WHERE Id = @AdminId) == 1; ";

                return Sql.MasterNonQueryAsync(query, args);
            }
            catch (Exception ex)
            {
                Worker.LogError($"Fehler DeleteUser() {ex}");
                return false;
            }
        }

        #endregion

        #region Datenquellenverwaltung

        internal static Dictionary<string, Plc> GetAllPlc()
        {
            string query = @" 
                SELECT Name, CpuType, Ip, Rack, Slot FROM Source
                WHERE ConnectionType == 1
                ; ";

            DataTable dt = Sql.MasterSelectDataTable(query, []);

            return dt.AsEnumerable()
                .ToDictionary<DataRow, string, Plc>(
                    row => row.Field<string>(0) ?? string.Empty,
                    row => new Plc(
                        (CpuType)row.Field<Int64>(1),
                        row.Field<string>(2) ?? string.Empty,
                        (short)row.Field<Int64>(3),
                        (short)row.Field<Int64>(4)
                        )
                    );
        }

        internal static bool CreateOrUpdatePlc(Dictionary<string, string> form, User admin)
        {

#if DEBUG
            Worker.LogWarning(string.Join(' ', form));
#endif

            if (admin.Name.Length < 2)
                return false;

            #region Formular auslesen
            string cpuid = WebUtility.UrlDecode(form["cpuid"]) ?? string.Empty;
            string cpuname = WebUtility.UrlDecode(form["cpuname"]) ?? string.Empty;
            int connectiontype = Convert.ToInt32(WebUtility.UrlDecode(form["connectiontype"]));
            S7.Net.CpuType cputype = (S7.Net.CpuType)Convert.ToInt32(WebUtility.UrlDecode(form["cputype"]));
            string cpuip = WebUtility.UrlDecode(form["ip"]) ?? string.Empty;
            int cpuport = Convert.ToInt32(WebUtility.UrlDecode(form["port"]));
            short cpurack = Convert.ToInt16(WebUtility.UrlDecode(form["rack"]));
            short cpuslot = Convert.ToInt16(WebUtility.UrlDecode(form["slot"]));
            string cpucomment = WebUtility.UrlDecode(form["comment"]);

            bool success = false;
            #endregion

            Plc plc = new(cputype, cpuip, cpurack, cpuslot);

            #region Cpu erstellen oder ändern

            //TODO: Cpu name nicht änderbar machen, wenn die CPU irgendwo verwendte wird!

            if (!string.IsNullOrWhiteSpace(cpuname) && plc is not null)
            {
                Dictionary<string, object> args = new()
                {
                    { "@Name", cpuname },
                    { "@ConnectionType", connectiontype },
                    { "@CpuType", cputype },
                    { "@Ip", cpuip },
                    { "@Port", cpuport },
                    { "@Rack", cpurack },
                    { "@Slot", cpuslot },
                    { "@Comment", cpucomment },
                    { "@AdminId", admin.Id }
                };

                string query = @" 
                INSERT INTO Source (Name, ConnectionType, CpuType, Ip, Port, Rack, Slot, Comment) VALUES (
                @Name, @ConnectionType, @CpuType, @Ip, @Port, @Rack, @Slot, @Comment)
                ON CONFLICT(Name) DO UPDATE SET
                    Name = @Name,
                    ConnectionType = @ConnectionType,
                    CpuType = @CpuType,
                    Ip = @Ip,
                    Port = @Port,
                    Rack = @Rack,
                    Slot = @Slot,
                    Comment = @Comment
                    WHERE (SELECT IsAdmin FROM User WHERE Id = @AdminId) == 1
                ; ";

                success = Sql.MasterNonQueryAsync(query, args);
            }

            #endregion

            #region Änderungen live schalten
            if (success && MyS7.Sps.ContainsKey(cpuname) && plc is not null)
            {
                MyS7.Sps[cpuname] = plc;
            }
            #endregion 

            return success;
        }

        internal static bool DeletePlc(Dictionary<string, string> form, User admin)
        {
#if DEBUG
            Worker.LogWarning(string.Join(' ', form));
#endif

            if (admin.Name.Length < 2)
                return false;

            #region Formular auslesen
            string cpuid = WebUtility.UrlDecode(form["cpuid"]) ?? string.Empty;
            string cpuname = WebUtility.UrlDecode(form["cpuname"]) ?? string.Empty;

            bool success = false;
            #endregion

            if (!string.IsNullOrWhiteSpace(cpuname))
            {
                Dictionary<string, object> args = new()
                {
                    { "@Name", cpuname },
                    { "@Id", cpuid },
                    { "@AdminId", admin.Id }
                };

                //Der angemeldete Benutzer muss ein Administrator sein und kann sich nicht selbst löschen.                
                string query = @"
                    DELETE FROM Source 
                    WHERE Id = @Id 
                    AND (SELECT IsAdmin FROM User WHERE Id = @AdminId) == 1; ";

                success = Sql.MasterNonQueryAsync(query, args);
            }

            return success;
        }

        #endregion

      

    }


    public class User
    {
        public User()
        {
            Id = 0;
            Name = string.Empty;
            Auth = string.Empty;
            UserRole = 0;
        }

        public User(int id, string name, string auth, Role role) {
            Id = id;
            Name = name;
            Auth = auth;
            UserRole = role;
        }

        public User(DataTable dt)
        {
            if (dt.Rows.Count < 1)
            {
                Id = 0;
                Name = string.Empty;
                Auth = string.Empty;
                UserRole = 0;
                return;
            }
               
            if (int.TryParse(dt.Rows[0]["Id"].ToString(), out int id))
            Id = id;

            Name = dt.Rows[0]["Name"].ToString() ?? string.Empty;
            Auth = dt.Rows[0]["Identification"].ToString() ?? string.Empty;

            if (int.TryParse(dt.Rows[0]["IsAdmin"].ToString(), out int role))
                UserRole = (Role)role;
        }

        public enum Role
        {
            User = 0,
            Administrator = 1
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Auth { get; set; }
        public Role UserRole { get; set; }

    }

}
