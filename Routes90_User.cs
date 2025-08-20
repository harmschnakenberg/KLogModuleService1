using Grapevine;
using System.Net;

namespace KLogModuleService1
{

    [RestResource(BasePath = "/config/user")]
    public partial class ConfigRoutes
    {


        #region Benutzerverwaltung

        [RestRoute("Get")]
        public static async Task UserManagement(IHttpContext context)
        {
            await UserManagement(context, string.Empty);
        }

        internal static async Task UserManagement(IHttpContext context, string message)
        {
            User user = Sql.LogedInUser(context) ?? new User();

            if (string.IsNullOrWhiteSpace(user.Name))
            {
                Worker.LogWarning("UserManagement() kein Benutzer angemeldet");
                await Routes.Home(context);
                return;
            }

            bool isAdmin = user.UserRole == User.Role.Administrator;

            string userTable = string.Empty;
            if (isAdmin) userTable = Sql.GetAllUsers(); //Nur Administratoren sehen alle Benutzer

            string script = @"
                <script>
                  function Select(tr){
                    document.forms['userform']['userid'].value = tr.childNodes[0].innerHTML;    
                    document.forms['userform']['username'].value = tr.childNodes[1].innerHTML;                                   
                    
                    switch (tr.childNodes[2].innerHTML) {
                      case '0':
                        document.forms['userform']['radio0'].checked = true;
                        break;
                      case '1':
                        document.forms['userform']['radio1'].checked = true;
                        break;   
                    }                   
                  }
                </script>";

            string form = @$"
                <div class='container mt-3'>
                <h3>Benutzerverwaltung</h3>
                <form action='/config/user/update' method='post' id='userform'>

                   <input type='hidden' id='userid' name='userid' value='{user.Id}'>

                   <div class='form-floating mb-3 mt-3'>
                     <input type='text' class='form-control' id='username' placeholder='Benutzername' name='username' value='{user.Name}' required {(isAdmin ? string.Empty : "readonly")}>
                     <label for='username'>Benutzername</label>
                   </div>

                   <div class='form-floating mb-3 mt-3'>
                     <input type='password' class='form-control' id='userpswd' placeholder='Passwort' name='userpswd'>
                     <label for='password'>Passwort</label>
                   </div>
            
                    <div class='mb-3 mt-3 form-check'>
                      <input type='radio' class='form-check-input' id='radio0' name='isadmin' value='0' checked {(isAdmin ? string.Empty : "disabled")}>
                      <label class='form-check-label' for='radio0'>Benutzer</label>
                    </div>
                    <div class='mb-3 mt-3 form-check'>
                      <input type='radio' class='form-check-input' id='radio1' name='isadmin' value='1' {(isAdmin ? string.Empty : "disabled")}>
                      {(isAdmin ? string.Empty : $"<input type='hidden' name='isadmin' value='{(int)user.UserRole}'>")}
                      <label class='form-check-label' for='radio1'>Administrator</label>
                    </div>

                  <button type='submit' class='btn btn-primary' {(user.Id > 0 ? string.Empty : "disabled")}>
                    Speichern
                  </button>
                  <button type='button' class='btn btn-danger' data-bs-toggle='modal' data-bs-target='#deleteModal' 
                     onclick=""document.getElementById('deluser').innerHTML = document.getElementById('username').value;"" {(isAdmin ? string.Empty : "disabled")}>
                    Löschen                  
                  </button>

                <!-- The Modal -->
                <div class='modal' id='deleteModal'>
                  <div class='modal-dialog'>
                    <div class='modal-content'>

                      <!-- Modal Header -->
                      <div class='modal-header'>
                        <h4 class='modal-title'>Benutzer wirklich löschen?</h4>
                        <button type='button' class='btn-close' data-bs-dismiss='modal'></button>
                      </div>

                      <!-- Modal body -->
                      <div class='modal-body'>
                        Den Benutzer <b id='deluser'></b> wirklich löschen?
                      </div>

                      <!-- Modal footer -->
                      <div class='modal-footer'>                        
                        <button type='submit' class='btn btn-danger' formaction='/config/user/delete' {(isAdmin ? string.Empty : "disabled")}>
                          Endgültig löschen
                        </button>
                        <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Abbrechen</button>
                      </div>

                    </div>
                  </div>
                </div>

                </form>
                </div>";

            string content = message + userTable + form;
            if (user.UserRole == User.Role.Administrator) content += script;

            await context.Response.SendResponseAsync(content.PageBody("Benutzerübersicht", user.Name));
        }

        [RestRoute("Post", "/update")]
        public async Task CreateOrUpdateUser(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);

            Dictionary<string, string> formContent = context.Locals["FormData"] as Dictionary<string, string> ?? [];
            int userid = Convert.ToInt32(WebUtility.UrlDecode(formContent["userid"]));
            string username = WebUtility.UrlDecode(formContent["username"]);

            if (user.UserRole != User.Role.Administrator && user.Id != userid)
            {
                //Wenn der Benutzer kein Adminstrator ist oder sich nicht selbst bearbeitet
                string alert = @"
                <div class='alert alert-danger'>
                    <strong>Nicht zulässig</strong>
                    Die Benutzerverwaltung kann nur durch einen Administrator geändert werden.
                </div>";
                await context.Response.SendResponseAsync(Html.LogInForm(alert).PageBody("Keine Berechtigung"));
                return;
            }

            bool success = Sql.CreateOrUpdateUser(formContent, user);

            string message = @"
                <div class='alert alert-danger'>
                    <strong>Abbruch</strong>
                    Der Benutzer konnte nicht geändert werden.
                </div>";

            if (success)
                message = @$"
                <div class='alert alert-success'>
                    <strong>Benutzerändeurng erfolgreich</strong>
                    Der Benutzer <b>[{userid}] {username}</b> wurde angepasst.
                </div>";

            await UserManagement(context, message);
        }

        [RestRoute("Post", "/delete")]
        public async Task DeleteUser(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);

            if (user?.UserRole != User.Role.Administrator)
            {
                string alert = @"
                <div class='alert alert-danger'>
                    <strong>Nicht zulässig</strong>
                    Die Benutzer können nur durch einen Administrator gelöscht werden.
                </div>";
                await context.Response.SendResponseAsync(Html.LogInForm(alert).PageBody("Keine Berechtigung"));
                return;
            }

            Dictionary<string, string> formContent = context.Locals["FormData"] as Dictionary<string, string> ?? [];
            bool success = Sql.DeleteUser(formContent, user);

            string message = @"
                <div class='alert alert-danger'>
                    <strong>Abbruch</strong>
                    Der Benutzer konnte nicht gelöscht werden.
                </div>";

            if (success)
                message = @$"
                <div class='alert alert-success'>
                    <strong>Benutzer erfolgreich entfernt</strong>
                    Der Benutzer <b>[{formContent["userid"]}] {formContent["username"] ?? "???"}</b> wurde gelöscht.
                </div>";

            await UserManagement(context, message);
        }

        #endregion

    }



}



//// MAGAZIN
///
//[RestResource(BasePath = "/cookie")]
//public class CookieResource
//{
//    [RestRoute("Get", "/set/{name}/{value}")]
//    public async Task SetCookie(IHttpContext context)
//    {
//        var name = context.Request.PathParameters["name"];
//        var value = context.Request.PathParameters["value"];

//        context.Response.Cookies.Add(new Cookie(name, value, "/"));
//        await context.Response.SendResponseAsync(Grapevine.HttpStatusCode.Ok);
//    }

//    [RestRoute("Get", "/get/{name}")]
//    public async Task GetCookie(IHttpContext context)
//    {
//        var name = context.Request.PathParameters["name"];
//        var cookie = context.Request.Cookies.Where(c => c.Name == name).FirstOrDefault();
//        await context.Response.SendResponseAsync($"Cookie Value: {cookie?.Value}");
//    }

//}