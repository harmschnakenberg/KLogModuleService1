using S7.Net;
using S7.Net.Types;
using System;
using System.Collections;
using System.Collections.Generic;
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
                int end = TagList[cpuname].Count;

                while (index < end) //Es können max. 20 Tags in einer Abfrage sein
                {
                    #region max. 20 Tags in einer Abfrage
                    int count = Math.Min(end - index, 20);
                    List<DataItem> range = TagList[cpuname].GetRange(index, count);
                    index += count;
                    #endregion

                    _ = await Sps[cpuname].ReadMultipleVarsAsync(range, cancelToken);

                    SaveTags(cpuname, range);
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
                _ = TagValues.TryGetValue(tagName, out object? value);
                
                if (Math.Abs(Convert.ToSingle(value) - Convert.ToSingle(item?.Value)) < 0.01) //Toleranz / LogBand
                    continue;
#if DEBUG
                Worker.LogInfo($"{tagName} = {item?.Value} (alt {value})");
#endif
                QueryBuilder.Append($"INSERT INTO Data (TagName, TagValue) VALUES ('{tagName}', {item?.Value}); ");
                TagValues[tagName] = item?.Value;

            }

        }


    }
    
}

