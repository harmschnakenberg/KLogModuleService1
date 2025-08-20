using Grapevine;
using HttpMultipartParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace KLogModuleService1
{
    [RestResource(BasePath = "/config/tag")]
    public partial class ConfigRoutes
    {

        //[Header("Content-Type", "multipart/form-data")]
        [RestRoute("Post", "/update")]
        public async Task TagImportUpdate(IHttpContext context)
        {
   
            string html = "<h1>Nicht implementiert</h1>" +
                "<div>Der Import von Variablen über eine Textdatei ist in Entwicklung</div>";

            try
            {
                Stream input = context.Request.InputStream;
                //Quelle: https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser
               // var parser = await MultipartFormDataParser.ParseAsync(input).ConfigureAwait(false);

                // From this point the data is parsed, we can retrieve the
                // form data using the GetParameterValue method.              
               // var option = parser.GetParameterValue("optradio");

                //option1 = vorhandene Variablen nicht verändern
                //option2 = vorhandene Variablen überschreiben
                //option3 = alle vorhandenen Variablen löschen

                // Files are stored in a list:
                //var file = parser.Files.First();
                //string filename = file.FileName;
                //Stream data = file.Data;
                //string fileContents;

                //using (StreamReader reader = new StreamReader(data))
                //{
                //    fileContents = await reader.ReadToEndAsync();
                //}

                //html += fileContents;
            }
            catch (Exception ex)
            {

                html += ex.Message;
            }
            
            await context.Response.SendResponseAsync(html.PageBody("K-Log Tags importiert"));
        }


        [RestRoute("Get", "/import")]
        public async Task TagImport(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);

            if (string.IsNullOrWhiteSpace(user.Name))
            {
#if DEBUG
                Worker.LogWarning("UserManagement() kein Benutzer angemeldet");
#endif
                await Routes.Home(context);
                return;
            }

            //https://transloadit.com/devtips/implementing-file-uploads-with-bootstrap-5/

            string html = @"
            <form action='/config/tag/update' method='post' >
              <div class='mb-3 container'>
                <h2 class='text-warning'>Baustelle</h2>
                <label for='formFile' class='form-label'>Import von Variablen-Beschreibungen</label>
                  <div id='dropZone' class='rounded border border-primary p-4 text-center'>
                    Hier klicken oder CSV-Datei mit Variableninformationen hierher ziehen
                  </div>
                  <div id='errorMessage' class='alert alert-danger d-none mt-2'></div>
                <input class='d-none' type='file' id='formFile' name='file' accept='.csv'>

                <span class='text-muted'>CSV Datei im Format <i>Variablenname;Variablenkommentar</i> hochladen.</span>
                <div class='form-check'>
                  <input type='radio' class='form-check-input' id='radio1' name='optradio' value='option1' checked>
                   vorhandene Variablen nicht verändern
                  <label class='form-check-label' for='radio1'></label>
                </div>
                <div class='form-check'>
                  <input type='radio' class='form-check-input' id='radio2' name='optradio' value='option2'>
                    vorhandene Variablen überschreiben
                  <label class='form-check-label' for='radio2'></label>
                </div>
                <div class='form-check'>
                  <input type='radio' class='form-check-input' id='radio3' name='optradio' value='option3'>
                    alle vorhandenen Variablen löschen
                  <label class='form-check-label'></label>
                </div> 

                <div class='mb-3 d-grid'>
                  <button type='submit' class='btn btn-primary btn-block'>Hochladen</button>
                </div> 
              </div>
            </form>

            <script>
              const dropZone = document.getElementById('dropZone')
              const formFile = document.getElementById('formFile')
              const errorMessage = document.getElementById('errorMessage')

              const allowedTypes = ['.csv', 'text/csv', 'application/vnd.ms-excel']
              const maxSize = 5 * 1024 * 1024 // 5MB

              function validateFile(file) {
                if (!allowedTypes.includes(file.type)) {
                  throw new Error('Ungültiger Dateityp ""' + file.type + '"". Bitte CSV-Datei hochladen.')
                }
                if (file.size > maxSize) {
                  throw new Error('Datei zu groß. Maximale Dateigröße 5MB.')
                }
              }

              function showError(message) {
                errorMessage.textContent = message
                errorMessage.classList.remove('d-none')
              }

              function hideError() {
                errorMessage.classList.add('d-none')
              }

              dropZone.addEventListener('click', () => formFile.click())

              dropZone.addEventListener('dragover', (e) => {
                e.preventDefault()
                dropZone.classList.add('bg-light')
              })

              dropZone.addEventListener('dragleave', () => {
                dropZone.classList.remove('bg-light')
              })

              dropZone.addEventListener('drop', (e) => {
                e.preventDefault()
                dropZone.classList.remove('bg-light')
                hideError()

                try {
                  const file = e.dataTransfer.files[0]
                  validateFile(file)
                  formFile.files = e.dataTransfer.files
                  updateDropZoneText()
                } catch (error) {
                  showError(error.message)
                }
              })

              formFile.addEventListener('change', () => {
                hideError()
                try {
                  if (formFile.files.length > 0) {
                    validateFile(formFile.files[0])
                    updateDropZoneText()
                  }
                } catch (error) {
                  showError(error.message)
                  formFile.value = ''
                }
              })

              function updateDropZoneText() {
                dropZone.textContent =
                  formFile.files.length > 0
                    ? formFile.files[0].name
                    : 'CSV-Datei heriher ziehen oder anklicken zur Auswahl'
              }
            </script>
            ";


            await context.Response.SendResponseAsync(html.PageBody("K-Log Import"));
        }


        [RestRoute]
        //[Header("Content-Type", "multipart/form-data")]
        public static async Task TagHome(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);
            bool isAdmin = user.UserRole == User.Role.Administrator;

            //var parser = await MultipartFormDataParser.ParseAsync(context.Request.InputStream);
            string info = "<h1>Hauptroute Tag</h1>";

            info += Html.DataTableToHtml(Sql.GetAllTags());

            string form = @"
                <div class='container mt-3'>
                <h3>Variablen zur Aufzeichnung hinzufügen</h3>
                <form action='/config/tag' method='post' id='tagform'>";

            for (int i = 0; i < 10; i++)
            {
                form += @$"<div class='input-group mb-3'>
                  <span class='input-group-text'>{i}</span>
                  <span class='input-group-text'>Name</span>
                  <input type='text' class='form-control' name='TagName{i}' placeholder='A01_DB10_DBW6' {(isAdmin ? string.Empty : "disabled")}>
                  <span class='input-group-text'>Datentyp</span>
                     <select class='form-select' name='TagType{i}'>
                      <option value='disc'>Diskret</option>
                      <option value='int'>Ganzzahl</option>
                      <option value='real'>Gleitkommazahl</option>
                      <option value='mem'>Text</option>
                    </select> 
                  <span class='input-group-text'>Beschreibung</span>
                  <input type='text' class='form-control' name='TagComment{i}' placeholder='Was macht dieser Datenpunkt?' {(isAdmin ? string.Empty : "disabled")}>
                </div>";
            }

            form += @$"<button type='submit' class='btn btn-primary' {(user.Id > 0 ? string.Empty : "disabled")}>
                    Speichern
                  </button>
              
                </form>
                </div>";

            await context.Response.SendResponseAsync(info.PageBody("K-Log Tagverwaltung", user.Name));
        }
    
    }
}
