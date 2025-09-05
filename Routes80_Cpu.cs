using Grapevine;
using System.Data;

namespace KLogModuleService1
{

    [RestResource(BasePath = "/config")]
    public partial class ConfigRoutes
    {

        #region Datenquellen (SPS, später ggf. OPCUA)

        /// <summary>
        /// Erzeugt neue Stammdaten zu einer Steuerung
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [RestRoute("Post", "/source/update")]
        public async Task CreateOrUpdatePlc(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);

#if DEBUG
            Worker.LogInfo("Eingeloggt ist " + user.Name + user.Auth);
#endif
            if (user.UserRole != User.Role.Administrator)
            {
                //Wenn der Benutzer kein Adminstrator ist
                string alert = @"
                <div class='alert alert-danger'>
                    <strong>Nicht zulässig</strong>
                    Diese Einstellung kann nur durch einen Administrator geändert werden.
                </div>";
                await context.Response.SendResponseAsync(Html.LogInForm(alert).PageBody("Keine Berechtigung"));
                return;
            }

            Dictionary<string, string> formContent = context.Locals["FormData"] as Dictionary<string, string> ?? [];
            bool success = Sql.CreateOrUpdatePlc(formContent, user);

            string message = @"
                <div class='alert alert-danger'>
                    <strong>Abbruch</strong>
                    Deie Datenquelle konnte nicht geändert werden.
                </div>";

            if (success)
                message = @$"
                <div class='alert alert-success'>
                    <strong>Datenquelle geändert</strong>
                    Die Datenquelle <b>{formContent["cpuname"]}</b> wurde von {user.Name} angepasst.
                </div>";

            await ConfigSource(context, message);
        }


        [RestRoute("Post", "/source/delete")]
        public async Task DeletePlc(IHttpContext context)
        {
            User user = Sql.LogedInUser(context) ?? new User();

            if (user.UserRole != User.Role.Administrator)
            {
                //Wenn der Benutzer kein Adminstrator ist
                string alert = @"
                <div class='alert alert-danger'>
                    <strong>Nicht zulässig</strong>
                    Diese Einstellung kann nur durch einen Administrator geändert werden.
                </div>";
                await context.Response.SendResponseAsync(Html.LogInForm(alert).PageBody("Keine Berechtigung"));
                return;
            }

            Dictionary<string, string> formContent = context.Locals["FormData"] as Dictionary<string, string> ?? [];
            bool success = Sql.DeletePlc(formContent, user);

            string message = @"
                <div class='alert alert-danger'>
                    <strong>Abbruch</strong>
                    Die Datenquelle konnte nicht gelöscht werden.
                </div>";

            if (success)
                message = @$"
                <div class='alert alert-success'>
                    <strong>Datenquelle erfolgreich entfernt</strong>
                    Der Datenquelle <b>[{formContent["cpuid"]}] {formContent["cpuname"] ?? "???"}</b> wurde gelöscht.
                </div>";

            await ConfigSource(context, message);
        }

        [RestRoute("Get", "/source")]
        public static async Task ConfigSource(IHttpContext context)
        {
            await ConfigSource(context, string.Empty);
        }

        internal static async Task ConfigSource(IHttpContext context, string message)
        {
            User user = Sql.LogedInUser(context) ?? new User();

            if (string.IsNullOrWhiteSpace(user.Name))
            {
#if DEBUG
                Worker.LogWarning("UserManagement() kein Benutzer angemeldet");
#endif
                await Routes.Home(context);
                return;
            }

            #region Alle bekannten SPSen auflisten

            DataTable table = Sql.MasterSelectDataTable(@"SELECT Id, Name, ConnectionType AS Verbindung, CpuType||'' AS CpuType, Ip, Port, Rack, Slot, Comment AS Bemerkung FROM Source", []);
            #endregion

            #region Tabellenwerte ersetzen

            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                table.Rows[rowIndex]["CpuType"] = Enum.GetName(typeof(S7.Net.CpuType), int.Parse(table.Rows[rowIndex]["CpuType"].ToString() ?? string.Empty));
                //TODO: durch anderen Datentyp erstezten ixt nicht zulässig
                //table.Rows[rowIndex]["Verbindung"] = Enum.GetName(typeof(Sql.CpuConnectionType), int.Parse(table.Rows[rowIndex]["Verbindung"].ToString() ?? string.Empty));
            }

            #endregion

            string script = @"
                <script>
                  function Select(tr){
                    document.forms['spsform']['cpuid'].value = tr.childNodes[0].innerHTML;
                    document.forms['spsform']['cpuname'].value = tr.childNodes[1].innerHTML;
                    document.getElementById(tr.childNodes[2].innerHTML).selected=true;  
                    document.getElementById(tr.childNodes[3].innerHTML).selected=true;                    
                    document.forms['spsform']['ip'].value = tr.childNodes[4].innerHTML;
                    document.forms['spsform']['port'].value = tr.childNodes[5].innerHTML;
                    document.forms['spsform']['rack'].value = tr.childNodes[6].innerHTML;
                    document.forms['spsform']['slot'].value = tr.childNodes[7].innerHTML;
                    document.forms['spsform']['comment'].innerHTML = tr.childNodes[8].innerHTML;
                  }
                </script>";

            string html = Html.DataTableToHtml(table, script);

            string form = @"
                <div class='container mt-3'>
                <h3>SPS Verbindung</h3>
                <form action='/config/source/update' method='post' id='spsform'>

                   <input type='hidden' name='cpuid' id='cpuid' value=''>

                   <div class='form-floating mb-3 mt-3'>
                     <input type='text' class='form-control' id='cpuname' placeholder='A01' name='cpuname' value='A01' required>
                     <label for='cpuname'>interner Name der SPS</label>
                   </div>

                   <div class='form-floating mb-3 mt-3'>
                    <select class='form-select' id='connectiontype' name='connectiontype'>
                      <option id='S7' value='1'>S7</option>    
                    </select>
                    <label for='spstype' class='form-label'>Siemens SPS Serie</label>
                   </div>

                   <div class='form-floating mb-3 mt-3'>
                    <select class='form-select' id='cputype' name='cputype'>
                      <option id='S7200' value ='0'>S7-200</option>
                      <option id='S7300' value ='10'>S7-300</option>
                      <option id='S7400' value ='20'>S7-400</option>
                      <option id='S71200' value ='30'>S7-1200</option>
                      <option id='S71500' value ='40'>S7-1500</option>
                    </select>
                    <label for='spstype' class='form-label'>Siemens SPS Serie</label>
                   </div>

                   <div class='form-floating mb-3 mt-3'>
                     <input type='text' class='form-control' id='ip' placeholder='192.168.0.10' name='ip' value='192.168.0.10' required>
                     <label for='ip'>SPS Adresse</label>
                   </div>
                   <div class='form-floating mb-3 mt-3'>
                     <input type='number' class='form-control' id='port' placeholder='102' name='port' value='102' required>
                     <label for='port'>TCP Port</label>
                   </div>
                   <div class='form-floating mb-3 mt-3'>
                     <input type='number' class='form-control' id='rack' placeholder='0' name='rack' min='0' max='99' value='0' required>
                     <label for='rack'>Rack</label>
                   </div>
                   <div class='form-floating mb-3 mt-3'>
                     <input type='number' class='form-control' id='slot' placeholder='0' name='slot' min='0' max='99' value='0' required>
                     <label for='slot'>Slot</label>
                   </div>
                   <div class='form-floating mb-3 mt-3'>                     
                     <textarea class='form-control' rows='3' id='comment' name='comment'></textarea>
                     <label for='comment'>Bemerkungen</label>
                   </div>
                  <button type='submit' class='btn btn-primary'>Speichern</button>
                  <button type='submit' class='btn btn-danger'>Löschen</button>
                </form>
                </div>";

            string tagList = "<ul class='list-group container mt-3'>";
#if DEBUG
            foreach (string key in MyS7.TagValues.Keys)
            {
                tagList += $"<li class='list-group-item'>{key}&nbsp;<span class='badge bg-info'>{MyS7.TagValues[key]}</span></li>";
            }
#endif
            tagList += "</ul>";

            await context.Response.SendResponseAsync((html + form + tagList).PageBody("SPS Verbindung einrichten", user.Name));
        }

        #endregion


    }
}
