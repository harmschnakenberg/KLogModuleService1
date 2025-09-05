using S7.Net;
using S7.Net.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLogModuleService1
{
    internal class MyS7
    {
        /// <summary>
        /// Cpu-Name, Cpu
        /// </summary>
        public static Dictionary<string, Plc> Sps { get; private set; } = [];

        /// <summary>
        /// Cpu-Name, Liste abzurufender Datenpunkte
        /// </summary>
        public static Dictionary<string, List<DataItem>> TagList { get; private set; } = [];

        public static Dictionary<string, object?> TagValues { get; private set; } = [];


        private static readonly StringBuilder QueryBuilder = new();


        public static void AddCpu(Dictionary<string, Plc> dict)
        {
            foreach (var plc in dict)
            {
                if (!Sps.ContainsKey(plc.Key))
                    Sps.Add(plc.Key, plc.Value);

                Worker.LogInfo($"SPS {plc.Value.CPU.ToString()} '{plc.Key}' IP {plc.Value.IP} hinzugefügt.");
            }           
        }

        /// <summary>
        /// Alle bekannten Tags auslesen
        /// </summary>
        /// <param name="cancelToken"></param>
        internal static async void ReadAllTags(CancellationToken cancelToken)
        {
            QueryBuilder.Clear();

            foreach (var cpuname in Sps.Keys)
            {
#if DEBUG
               // Worker.LogWarning($"Verbinde mit {cpuname}, {Sps[cpuname].IP}, Rack {Sps[cpuname].Rack}, Slot {Sps[cpuname].Slot}");
#endif
                if (!Sps[cpuname].IsConnected) {
                    try
                    {
                        await Sps[cpuname].OpenAsync(cancelToken);
                    }
                    catch
                    {
                        Worker.LogError($"SPS {cpuname} an {Sps[cpuname].IP} nicht erreichbar.");
                        continue;
                    }
                }
                if (cancelToken.IsCancellationRequested) Sps[cpuname].Close();
                if (!Sps[cpuname].IsConnected) continue;

                int index = 0;
                int end = 0;

                if (TagList.ContainsKey(cpuname))
                    end = TagList[cpuname].Count;

                while (index < end) //Es können max. 20 Tags in einer Abfrage sein
                {
                    #region max. 20 Tags in einer Abfrage
                    int count = Math.Min(end - index, 20);
                    List<DataItem> range = TagList[cpuname].GetRange(index, count);
                    index += count;
                    #endregion

                    try
                    {
                        if (!Sps.ContainsKey(cpuname))
                            continue;

                        _ = await Sps[cpuname].ReadMultipleVarsAsync(range, cancelToken);

                        SaveTags(cpuname, range);
                    }
                    catch (Exception ex)
                    {
                        Worker.LogError($"SPS {cpuname}\r\n" +ex.ToString()+"\r\n");
                    }
                }
 
            }

            _ = Sql.DataNonQueryAsync(QueryBuilder.ToString());
                
        }

        /// <summary>
        /// Name im Format A01_DB10_DBW6 aus DataItem
        /// </summary>
        /// <param name="cpuName"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        private static string GetTagName(string cpuName, DataItem item)
        {
            string itemName = cpuName + "_";
            string offset = string.Empty;

            switch (item.VarType)
            {
                case VarType.Bit:
                    offset = $"DBX{item.StartByteAdr}_{item.BitAdr}";
                    break;
                case VarType.Byte:
                    offset = $"DBB{item.StartByteAdr}";
                    break;
                case VarType.Word:
                case VarType.Int:
                    offset = $"DBW{item.StartByteAdr}";
                    break;
                case VarType.DWord:
                case VarType.DInt:
                    break;
                case VarType.Real:
                    offset = $"DBD{item.StartByteAdr}";
                    break;
                    //case VarType.LReal:
                    //    break;
                    //case VarType.String:
                    //    break;
                    //case VarType.S7String:
                    //    break;
                    //case VarType.S7WString:
                    //    break;
                    //case VarType.Timer:
                    //    break;
                    //case VarType.Counter:
                    //    break;
                    //case VarType.DateTime:
                    //    break;
                    //case VarType.DateTimeLong:
                    //    break;
                    //default:
                    //    break;
            }

            itemName += item.DataType switch
            {
                DataType.DataBlock => $"DB{item.DB}_{offset}",
                DataType.Input => $"E{item.StartByteAdr}_{item.BitAdr}",
                DataType.Output => $"A{item.StartByteAdr}_{item.BitAdr}",
                DataType.Memory => $"M{item.StartByteAdr}_{item.BitAdr}",
                DataType.Timer => "Timer",
                DataType.Counter => "Counter",
                _ => "Unbekannt",
            };

#if DEBUG
            //Worker.LogInfo($"{itemName} = {item.Value}");
#endif
            return itemName;
        }

        /// <summary>
        /// Merke geänderte Werte für DB vor
        /// </summary>
        /// <param name="cpuName"></param>
        /// <param name="items"></param>
        private static void SaveTags(string cpuName, List<DataItem> items)
        {

            foreach (var item in items)
            {
                string tagName = GetTagName(cpuName, item);
                _ = TagValues.TryGetValue(tagName, out object? oldValue);
                
                if (Math.Abs(Convert.ToSingle(oldValue) - Convert.ToSingle(item?.Value)) < 0.01) //Toleranz / LogBand
                    continue;
#if DEBUG
               // Worker.LogInfo($"{tagName} = {item?.Value} (alt {oldValue})");
#endif
                QueryBuilder.Append($"INSERT INTO Data (TagName, TagValue) VALUES ('{tagName}', {item?.Value}); ");
                TagValues[tagName] = item?.Value;

            }

        }

        internal static DataItem GetDataItem(string tagName, out string cpuName)
        {
            string[] part = tagName.Split('_');
            cpuName = part[0];

            #region Datentyp

            DataType dataType = DataType.DataBlock;            
            VarType varType = VarType.Byte;

            switch (part[1].Substring(0, 1))
            {
                case "E":
                    dataType = DataType.Input;
                    varType = VarType.Bit;
                    break;
                case "A":
                    dataType = DataType.Output;
                    varType = VarType.Bit;
                    break;
                case "M":
                    dataType = DataType.Memory;
                    varType = VarType.Bit;
                    break;
            }
            ;
            #endregion


            #region Variablentyp //TODO: noch nicht sauber!

            //A01_DB10_DBX10_1
            //A01_DB10_DBW2
            //A01_E1_7

        

            switch (part[2].Substring(0, 3))
            {
                case "DBX":
                    varType = VarType.Bit;
                    break;
                case "DBB":
                    varType = VarType.Byte;
                    break;
                case "DBW":
                    varType = VarType.Word;
                    break;
                case "DBD":
                    varType = VarType.Real;
                    break;
            }


            #endregion


            DataItem dataItem = new DataItem();

            dataItem.DataType = dataType;

            if (dataType == DataType.DataBlock && part.Length > 1)
                dataItem.DB = Convert.ToInt32(part[1].Substring(2));
            else
                dataItem.DB = Convert.ToInt32(part[1].Substring(1));

            dataItem.VarType = varType;

            if (varType == VarType.Bit && part.Length > 3)
                dataItem.BitAdr = Convert.ToByte(part[3]);

            if(part.Length > 2)
            dataItem.StartByteAdr = Convert.ToInt32(part[2].Substring(3));

            //Wenn es diese CPU nicht gibt, ungültigen Wert schreiben
            if (!MyS7.Sps.ContainsKey(cpuName))
                cpuName = string.Empty;

            return dataItem;
        }


        /// <summary>
        /// Liest aus einer Tabelle die Spalte 'TagNames' und fügt sie der Liste abgefragter DtaItems hinzu
        /// </summary>
        /// <param name="dt"></param>
        internal static void ReadTags(DataTable dt)
        {
            List<string?> tagNames = dt.AsEnumerable().Select(x => x["TagName"].ToString()).ToList();
#if DEBUG
            Worker.LogInfo("Datenpunkte werden ausgelesen: " + string.Join(',', tagNames));
#endif
            foreach (string? tagName in tagNames)
            {
                if (string.IsNullOrEmpty(tagName))
                    continue;

                DataItem dataItem = GetDataItem(tagName, out string cpuName);

                if (!TagList.ContainsKey(cpuName))
                    TagList.Add(cpuName, new List<DataItem>());

                    TagList[cpuName].Add(dataItem);
            }

        }

    }
    
}

