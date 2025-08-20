using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLogModuleService1
{
    internal static class Html
    {
        /// <summary>
        /// Kreutzträger-Logo
        /// </summary>
        public const string Logo = @"<svg height='35' width='37'>
                                    <style>svg {background-color:#dddddd;margin-right:10px;}</style>
                                    <line x1='0' y1='0' x2='0' y2='35' style='stroke:darkcyan;stroke-width:2' />
                                    <polygon points='10,0 10,15 25,0' style='fill:#00004d;' />
                                    <polygon points='10,20 10,35 25,35' style='fill:#00004d;' />
                                    <polygon points='20,17 37,0 37,35' style='fill:darkcyan;' />
                                    Sorry, your browser does not support inline SVG.
                                  </svg>";


        /// <summary>
        /// Ausklappbares Menü
        /// </summary>
        private static string Menue(string user)
        {
            return @"
          <div class='offcanvas offcanvas-end text-bg-dark' id='menu'>
            <div class='offcanvas-header'>
              <h2 class='offcanvas-title'>Men&uuml;</h2>
              <button type='button' class='btn-close btn-close-white' data-bs-dismiss='offcanvas'></button>
            </div>
            <ul class='offcanvas-body navbar-nav'>
              <li class='nav-item'>
                <a href='/' class='btn'>Login</a>
              </li>
              <li class='nav-item'>
                <a href='/config/user' class='btn'>Benutzer</a>
              </li>
              <li class='nav-item'>
                <a href='/config/source' class='btn'>Verbindung</a>
              </li>

              <li class='nav-item'>
                <a href='/config/tag/import' class='btn'>Variablen</a>
              </li>
              <li class='nav-item'>
                <a href='#' class='btn'>Link Button</a>
              </li>
            </ul>
          </div>
          
          <div class='container-fluid'>
            <button class='btn btn-lg position-absolute top-0 end-0' type='button' data-bs-toggle='offcanvas' data-bs-target='#menu'>
                <span>" + user + @"</span>
                <span class='navbar-toggler-icon'></span>
            </button>
          </div>";
        }


        /// <summary>
        /// Gerüst der HTML-Seite
        /// </summary>
        /// <param name="html"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        internal static string PageBody(this string html, string header, string user = "") => @"
            <!DOCTYPE html>
            <html lang='de' data-bs-theme='dark'>
            <head>
              <title>K-Log Modul</title>                
              <meta charset='utf-8'>  
              <meta name='viewport' content='width=device-width, initial-scale=1'>
              <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css' rel='stylesheet'>
              <link rel='shortcut icon' href='https://kreutztraeger-kaeltetechnik.de/wp-content/uploads/2021/11/kt-logo-favicon.ico' type='image/x-icon'>
              <script src='https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js'></script>
            </head>
            <body class='container-fluid'>
            <div class='card'>
              <div class='card-header'> 
                <span class='h3'>" + Logo + header + "</span>" + 
                Menue(user) + @"</div>
              <div class='card-body'>" + html + @"</div>
              <div class='card-footer'>" + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + @"</div>
            </div>
            </body>
            </html>";


        internal static string ErrorPage(string message)
        {
            string html = "<div class=\"alert alert-danger\">\r\n" +
                "    <strong>Fehler!</strong>\r\n" +
                message +
                "  </div>";

            return html.PageBody("Fehler");
        }


        public static string DataTableToHtml(DataTable dt)
        {
            if (dt.Rows.Count == 0) return string.Empty; 

            StringBuilder builder = new();
            builder.Append("<div class='container'>");
            builder.Append("<table class='table table-dark table-hover'>");
            builder.Append("<thead>"); 
            builder.Append("<tr>");
            
            foreach (DataColumn c in dt.Columns)            
                builder.Append("<th>" + c.ColumnName + "</th>");            

            builder.Append("</tr><tbody>");
            
            foreach (DataRow r in dt.Rows)
            {
                builder.Append("<tr onclick='Select(this);'>");
                foreach (DataColumn c in dt.Columns)                
                    builder.Append("<td>" + r[c.ColumnName] + "</td>");
                
                builder.Append("</tr>");
            }
            builder.Append("</tbody></table></div>");

            return builder.ToString();
        }

        
        internal static string LogInForm(string alert = "")
        {

            const string loginForm = @"
              <div class='container'>                         
                 <form action='/login' method='post' id='loginform'>
                  <div class='mb-3 mt-3'>
                    <label for='user' class='form-label'>Benutzer:</label>
                    <input type='text' class='form-control' id='user' placeholder='Mein Benutzername...' name='user'>
                  </div>
                  <div class='mb-3'>
                    <label for='pswd' class='form-label'>Passwort:</label>
                    <input type='password' class='form-control' id='pswd' placeholder='Mein Passwort...' name='pswd'>
                  </div>                 
                  <button type='submit' class='btn btn-primary'>Login</button>
                  <button type='submit' class='btn btn-secondary' formaction='/logout'>Logout</button>
                </form> 
                <p>
                  <div class='toast show'>
                    <div class='toast-header'>
                      <strong class='me-auto'>...we have Cookies</strong>
                      <button type='button' class='btn-close' data-bs-dismiss='toast'></button>
                    </div>
                    <div class='toast-body'>
                      <p>
                        Diese Seite verwendet Cookies für den Login.<br/>
                        Die Cookies werden nicht zur Analyse des Nutzungsverhaltens oder für Werbezwecke genutzt.
                      </p>
                    </div>
                  </div>
                </p>
              </div>
            ";

            const string notes = @"
              <p><h3>ToDo</h3>
              <b>erforderlich</b><ol>
              <li>Maske zur Eingabe der S7-Verbindungsparameter</li>
              <li>S7NetPlus Verbindung zur SPS</li>
              <li>SQLite Datenbank</li>
              <li>Maske zur Auswahl der Ausgabe (Variablenbeschreibung und Zeiten)</li>
              <li>EPPlus Excel-Export der gesammelten Daten</li>
              <li>Google Charts Kurvendarstellung</li>
              <li>...</li>
              <li>...</li>
              <li>...</li>
              <li>...</li>
              <li>Hardware für den Schaltschrank</li>
              </ol>
              <br/><b>wünschenswert</b><ol>
              <li>Einbinden mehrere SPSen als Datenquelle</li>
              <li>""sichere"" S7-Verbindung</li>
              <li>Modbus-Verbindung</li>
              <li>OPCUA-Verbindung</li>
              <li>JSON Schnittstelle für externe Daten-Consumer</li>
              <li>""Watchdog"" Verbindungsüberwachung zur SPS</li>
              <li>Backup-Funktion auf externen Datenträger / Netzlaufwerk</li>
              <li>...</li>
              <li>...</li>
              </ol>
              </p>";

            string accordion = @"
                <div class='container mt-3'>
                  <h1>K-Log Modul</h1>                 
                  <div id='accordion'>
                    <div class='card'>
                      <div class='card-header'>
                        <a class='btn' data-bs-toggle='collapse' href='#collapseOne'>
                          <h5>Login</h5>
                        </a>
                      </div>
                      <div id='collapseOne' class='collapse show' data-bs-parent='#accordion'>
                        <div class='card-body'>"
                        + alert + loginForm + @"
                        </div>
                      </div>
                    </div>
                    <div class='card'>
                      <div class='card-header'>
                        <a class='collapsed btn' data-bs-toggle='collapse' href='#collapseTwo'>
                        <h5>Relese Notes</h5>
                      </a>
                      </div>
                      <div id='collapseTwo' class='collapse' data-bs-parent='#accordion'>
                        <div class='card-body'>"
                        + notes + @"</div>
                      </div>
                    </div>                   
                  </div>
                </div>";

            return accordion;
        }

    }
}
