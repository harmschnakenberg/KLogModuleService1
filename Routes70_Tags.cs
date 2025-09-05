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
    [RestResource(BasePath = "/config")]
    public partial class ConfigRoutes
    {

        #region Import-File

        //[Header("Content-Type", "multipart/form-data")]
        [RestRoute("Post", "/tag/import/update", Description = "Änderungen über Import-File")]
        public async Task TagImportUpdate(IHttpContext context)
        {

            string html = "<h1>Nicht implementiert</h1>" +
                "<div>Der Import von Variablen über eine Textdatei ist in Entwicklung</div>";

            try
            {
                Dictionary<string, string> formContent = context.Locals["FormData"] as Dictionary<string, string> ?? [];
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


        [RestRoute("Get", "/tag/import")]
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
            <form action='/config/tag/import/update' method='post' >
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

        #endregion

        #region manuelle Manipulation von Tags

        [RestRoute("Post", "/tag/update", Description = "Änderungen über Formular auf Weboberfläche")]
        public async Task TagListUpdate(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);
            bool isAdmin = user.UserRole == User.Role.Administrator;

            string html = "<h3>Variablenliste geändert</h3>";

            if (!isAdmin)
                html += Html.ErrorPage("Keine Änderungen durchgeführt. Nur Administratoren können Variablen bearbeiten!");
            else
                try
                {
                    Stream input = context.Request.InputStream;

                    Dictionary<string, string> formContent = context.Locals["FormData"] as Dictionary<string, string> ?? [];
                    _ = Sql.CreateOrUpdateTags(formContent, user);
                }
                catch (Exception ex)
                {

                    html += ex.Message;
                }

            html += Html.DataTableToHtml(Sql.GetAllTags());

            await context.Response.SendResponseAsync(html.PageBody("K-Log Tags manipuliert"));
        }

        #endregion

        [RestRoute("Get", "/tag")]
        //[Header("Content-Type", "multipart/form-data")]
        public static async Task TagHome(IHttpContext context)
        {
            User user = Sql.LogedInUser(context);
            bool isAdmin = user.UserRole == User.Role.Administrator;

            string script = @"
                <script>
                  function Select(tr){
                    document.forms['tagform']['TagName0'].value = tr.childNodes[0].innerHTML;
                    document.forms['tagform']['TagType0'].value = tr.childNodes[1].innerHTML;
                    document.forms['tagform']['TagComment0'].value = tr.childNodes[2].innerHTML;                    
                  }
                </script>
               ";

            //TODO: hinzugefügte Zeilen sind vertikal versetzt, Button wandert immer weiter nach unten.
            string script_expandForm = @"<script>
                    function AddRow() {
                        const form = document.forms['tagform'];                        
                        const children = form.getElementsByTagName('div');        
                        const n = children.length;       
                        const div1 = document.createElement('div');
                        div1.classList.add('input-group');
                        div1.classList.add('mb-3');
                     
                        div1.appendChild(createSpan(n));
                        div1.appendChild(createSpan('Name'));
                        div1.appendChild(createInput('TagName' + n));
                        div1.appendChild(createSpan('Datentyp'));
                        div1.appendChild(createSelect('TagType' + n));
                        div1.appendChild(createSpan('Beschreibung'));
                        div1.appendChild(createTextArea('TagComment' + n));

                        children[children.length - 1].appendChild(div1);
                    }

                    function createSpan(txt){
                        const span1 = document.createElement('span');
                        span1.classList.add('input-group-text');
                        span1.innerHTML = txt ;
                        return span1;
                    }

                    function createInput(name1){
                        const input1 = document.createElement('input');
                        input1.setAttribute('type', 'text');
                        input1.setAttribute('name', name1);
                        input1.setAttribute('id', name1);                        
                        input1.classList.add('form-control');
                        input1.classList.add('shorty');                    
                        return input1;
                    }

                    function createSelect(name1){
                        const selectList = document.createElement('select');                        
                        selectList.setAttribute('name', name1);
                        selectList.setAttribute('id', name1);                        
                        selectList.classList.add('form-select');

                        var txt = ['Diskret','Ganzzahl','Gleitkommazahl','Text'];
                        var val = ['disc','int','real','mem'];
                        for (var i = 0; i < txt.length; i++) {
                            var option = document.createElement('option');
                            option.value = val[i];
                            option.text = txt[i];
                            selectList.appendChild(option);
                        }

                        return selectList;
                    }

                    function createTextArea(name1){
                        const txt = document.createElement('textarea');  
                        txt.setAttribute('name', name1);
                        txt.setAttribute('id', name1);     
                        txt.setAttribute('rows', 1);     
                        txt.classList.add('form-control');

                        return txt;
                    }

                </script>
                <button type='button' class='btn btn-primary' onclick='AddRow();'>+</button>
                ";

            string info = "<h1>Hauptroute Tag</h1>";

            info += Html.DataTableToHtml(Sql.GetAllTags(), script);

            string form = @$"
                <style>.input-group>input.shorty {{flex: 0 1 10em;}}</style>
                <div class='container mt-3'>
                <h3>Variablen zur Aufzeichnung hinzufügen</h3>
                <form action='/config/tag/update' method='post' id='tagform'>
                <button type='submit' class='btn btn-primary' {(user.Id > 0 ? string.Empty : "disabled")}>Speichern</button>";

            int i = 0;

            form += @$"<div class='input-group mb-3'>
                  <span class='input-group-text'>{i}</span>
                  <span class='input-group-text'>Name</span>
                  <input type='text' class='form-control shorty' name='TagName{i}' id='TagName{i}' placeholder='A01_DB10_DBW{i * 2}' {(isAdmin ? string.Empty : "disabled")}>
                  <span class='input-group-text'>Datentyp</span>
                     <select class='form-select' name='TagType{i}' id='TagType{i}'>
                      <option value='disc'>Diskret</option>
                      <option value='int'>Ganzzahl</option>
                      <option value='real'>Gleitkommazahl</option>
                      <option value='mem'>Text</option>
                    </select> 
                  <span class='input-group-text'>Beschreibung</span>
                  <textarea class='form-control' rows='1' id='TagComment{i}' name='TagComment{i}' id='TagComment{i}' placeholder='Was macht dieser Datenpunkt?' {(isAdmin ? string.Empty : "disabled")}></textarea>                  
                </div>";

            form += script_expandForm +
                    @$"</form></div>";



            //document.getElementById(tr.childNodes[2].innerHTML).selected=true;  
            //document.getElementById(tr.childNodes[3].innerHTML).selected=true;                    

            await context.Response.SendResponseAsync((info + form).PageBody("K-Log Tagverwaltung", user.Name));
        }

    }
}
