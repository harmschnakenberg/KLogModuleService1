using S7.Net;
using S7.Net.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KLogModuleService1
{
    internal partial class Sql
    {
        private static readonly StringBuilder QueryBuilder = new();

        #region Variablenmagazin

        internal static DataTable GetAllTags()
        {
            string query = @" 
                SELECT TagName, TagType, TagComment FROM TagNames
                ; ";

            return Sql.DataSelectDataTable(query, [], 30);
        }

        internal static bool CreateOrUpdateTags(Dictionary<string, string> form, User admin)
        {

#if DEBUG
            Worker.LogWarning(string.Join(' ', form));
#endif

            if (admin.Name.Length < 2)
                return false;

            StringBuilder QueryBuiler = new();
            List<string> NewTagNames = [];

            #region Formular auslesen

            for (int i = 0; i < 10; i++)
            {
                if (!form.ContainsKey($"TagName{i}"))
                    continue;

                string tagName = WebUtility.UrlDecode(form[$"TagName{i}"]);
                string tagType = WebUtility.UrlDecode(form[$"TagType{i}"]);
                string tagComment = WebUtility.UrlDecode(form[$"TagComment{i}"]);

                if (string.IsNullOrEmpty(tagName))
                    continue;

                QueryBuiler.Append($"INSERT OR IGNORE INTO TagNames (TagName, TagType, TagComment) VALUES ('{tagName}', '{tagType}', '{tagComment}'); " +
                                    $"UPDATE TagNames SET TagType = '{tagType}', TagComment = '{tagComment}' WHERE TagName = '{tagName}'; ");

                NewTagNames.Add(tagName);
            }

            Worker.MyMutex.WaitOne();

            #region Änderungen live schalten
            //ohne Mutex BÖSE: MyS7.TagList wird in der Worker-Schleife verwendet! Änderungen nur, wenn MyS7.ReadAllTags(stoppingToken); durch ist
            foreach (var tagName in NewTagNames)
            {
                DataItem dataItem = MyS7.GetDataItem(tagName, out string cpu);

                if (MyS7.Sps.ContainsKey(cpu))
                    MyS7.TagList[cpu].Add(dataItem);
            }

            #endregion

            Worker.MyMutex.ReleaseMutex();

            Worker.LogInfo(QueryBuilder.ToString());
            #endregion

            return Sql.DataNonQueryAsync(QueryBuiler.ToString());
        }

        #endregion



    }
}

/*
 *  MAGAZIN
 */

/*
 *         /// <summary>
        /// attachSQL = attach 'C:\\WOI\\Daily SQL\\Attak.sqlite' as db1 */
/// path = "Path of the sqlite database file
/// sqlQuery  = @"Select A.SNo,A.MsgDate,A.ErrName,B.SNo as BSNo,B.Err as ErrAtB from Table1 as A 
///                    inner join db1.Labamba as B on 
///                    A.ErrName = B.Err";
/// Quelle: https://stackoverflow.com/questions/6824717/sqlite-how-do-you-join-tables-from-different-databases                   
/// </summary>
/// <param name="attachSQL"></param>
/// <param name="sqlQuery"></param>
/*public static DataTable GetDataTableFrom2DBFiles(string attachSQL, string sqlQuery)
{
    try
    {
        string conArtistName = "data source=" + path + ";";
        using (SQLiteConnection singleConnectionFor2DBFiles = new SQLiteConnection(conArtistName))
        {
            singleConnectionFor2DBFiles.Open();
            using (SQLiteCommand AttachCommand = new SQLiteCommand(attachSQL, singleConnectionFor2DBFiles))
            {
                AttachCommand.ExecuteNonQuery();
                using (SQLiteCommand SelectQueryCommand = new SQLiteCommand(sqlQuery, singleConnectionFor2DBFiles))
                {
                    using (DataTable dt = new DataTable())
                    {
                        using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(SelectQueryCommand))
                        {
                            adapter.AcceptChangesDuringFill = true;
                            adapter.Fill(dt);
                            return dt;
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show("Use Process Exception method An error occurred");
        return null;
    }

}

*
* 
 * 
 */