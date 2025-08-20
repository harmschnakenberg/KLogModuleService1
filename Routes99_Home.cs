using Grapevine;
using HttpMultipartParser;
using System.Net;

namespace KLogModuleService1
{


    [RestResource(BasePath = "/chart")]
    public partial class ChartRoutes
    {

        [RestRoute]
        public static async Task TestChart(IHttpContext context)
        {

            //TODO: https://www.w3schools.com/js/js_graphics_plotly.asp
            // Plotly.js is free and open-source under the MIT license. It costs nothing to install and use. You can view the source, report issues and contribute using Github.

            // - mögliche TagNames aus der Datenbank auslesen und in ein Select-Feld schreiben.
            // - Stiftauswahl für 6 Stifte (ggf. Stiftanzahl dynamisch anpassbar?)
            // - Stiftauswahl speichern und einen Link dazu erstellen 
            // - Verwaltung der gespeicherten Stifte (Stifte, Wertebereich, Zeitbereich) Übersicht mit Kommentar, links)
            // - Anzeige von gespeichetren Charts übereinander (Kurve1, Kurve2, Statuskurve)
            // - 

            // - Wertebereich daynamisch anpassbar machen 

            // - Zeitbereich 8 Std. / 24 Std. / Frei
            // - Zeitenspeicher

            User user = Sql.LogedInUser(context);

            string html = @"
                <!DOCTYPE html>
                <html>
                <script src='https://cdn.plot.ly/plotly-latest.min.js'></script>

                <body>
                <div id='myPlot' style='width:100%;max-width:800px'></div>

                <script>
                let exp1 = 'x';
                let exp2 = '1.5*x*x';
                let exp3 = '1.5*x + 7';

                // Generate values
                const x1Values = [];
                const x2Values = [];
                const x3Values = [];
                const y1Values = [];
                const y2Values = [];
                const y3Values = [];

                for (let x = 0; x <= 10; x += 1) {
                  x1Values.push(x);
                  x2Values.push(x);
                  x3Values.push(x);
                  y1Values.push(eval(exp1));
                  y2Values.push(eval(exp2));
                  y3Values.push(eval(exp3));
                }

                // Define Data
                const data = [
                  {x: x1Values, y: y1Values, mode:'lines'},
                  {x: x2Values, y: y2Values, mode:'lines'},
                  {x: x3Values, y: y3Values, mode:'lines'}
                ];

                //Define Layout
                const layout = {title: '[y=' + exp1 + ']  [y=' + exp2 + ']  [y=' + exp3 + ']'};

                // Define Layout
                //const layout = {
                //  xaxis: {range: [40, 160], title: 'Square Meters'},
                //  yaxis: {range: [5, 16], title: 'Price in Millions'},
                //  title: 'House Prices vs Size'
                //};

                // Display using Plotly
                Plotly.newPlot('myPlot', data, layout);
                </script>

                </body>
                </html>
            ";

            await context.Response.SendResponseAsync(html.PageBody("K-Log Chartbaukasten", user.Name));
        }
    }

    [RestResource]
    public class Routes
    {

        [RestRoute("Post", "/login")]
        public async Task Login(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);

            #region Cookiee setzen
            if (!string.IsNullOrEmpty(user.Name))
            {
                Cookie cookie = new("auth", user.Auth, "/")
                {
                    Expires = DateTime.Now.AddDays(1)
                };
                context.Response.Cookies.Add(cookie);
            }
            #endregion

            #region Anzeige des Login-Erfolgs
            if (user.Name.Length > 0)
            {
                string success = $@"
                <div class='alert alert-success'>
                    <strong>Login erfolgreich</strong>
                    Der Zugang zum Modul ist für <b>{user.Name}</b> freigeschaltet.
                </div>";

                Worker.LogInfo($"'{user.Name}' wird eingeloggt.");
                await context.Response.SendResponseAsync(Html.LogInForm(success).PageBody("K-Log Login erfolgreich", user.Name));
                return;
            }
            else
            {
                string alert = @"
                <div class='alert alert-danger'>
                    <strong>Login fehlgeschlagen</strong>
                    Der Benutzer ist unbekannt, das Passwort ist falsch oder der Benutzer konnte nicht angemeldet werden.
                </div>";
                await context.Response.SendResponseAsync(Html.LogInForm(alert).PageBody("K-Log Login fehlgeschlagen", user.Auth));
                return;
            }
            #endregion
        }

        
        
        [RestRoute("Post", "/logout")]
        [RestRoute("Get", "/logout")]
        public async Task Logout(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);
            Worker.LogInfo($"'{user.Name}' wird ausgeloggt.");

            #region ungültigen Cookiee setzen

            Cookie cookie = new("auth", string.Empty, "/")
            {
                Expires = DateTime.Now.AddYears(-1)
            };
            context.Response.Cookies.Add(cookie);

            #endregion

            string alert = @$"
                <div class='alert alert-warning'>
                    <strong>Logout</strong>
                    Der Benutzer <b>{user.Name}</b> wurde abgemeldet.
                </div>";
            await context.Response.SendResponseAsync(Html.LogInForm(alert).PageBody("K-Log Logout"));
        }



        [RestRoute]
        public static async Task Home(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);

            string info = string.Empty;
#if DEBUG
            if (context.Request.Endpoint.Length > 0)
              info = $"Route <b style='color:red;'>{context.Request.Endpoint}</b>";
#endif

            await context.Response.SendResponseAsync((Html.LogInForm(info)).PageBody("K-Log Start", user.Name));
        }

    }

}
