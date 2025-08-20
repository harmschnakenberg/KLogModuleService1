//using S7.Net;
//using S7.Net.Types;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.NetworkInformation;
//using System.Text;
//using System.Threading.Tasks;
//using static KLogModuleService1.S7Class;

//namespace KLogModuleService1
//{
//    internal class S7Class
//    {
//        #region Felder

//        /// <summary>
//        /// CPU-Name, CPU-Objekt
//        /// </summary>
//        public static Dictionary<string, Plc> Sps { get; private set; } = [];

//        public static sbyte MaxTelegramSize { get; set; } = 20;


//        public enum CpuType
//        {
//            S7200 = 0,
//            S7300 = 10,
//            S7400 = 20,
//            S71200 = 30,
//            S71500 = 40,
//        }

//        #endregion

//        #region SPS Management

//        public static void AddCpu(string cpuName, CpuType cpuType, string Ip, short rack, short slot)
//        {
//            if (!Sps.ContainsKey(cpuName))
//                Sps.Add(cpuName, new Plc((S7.Net.CpuType)cpuType, Ip, rack, slot));
//        }

//        public static void AddCpu(Dictionary<string, Plc> dict)
//        {
//            foreach (var plc in dict)
//            {
//                if (!Sps.ContainsKey(plc.Key))
//                    Sps.Add(plc.Key, plc.Value);
//            }
//        }

//        #endregion

//        public static object? ReadTag(string tagName)
//        {
//            object? result = null;

//            tagName = tagName.Replace("Mittel_", "").Replace("Dif_", "");

//            var parts = tagName.Split('_');

//            string cpuName = parts[0];

//            if (!Sps.TryGetValue(cpuName, out Plc? sps)) //CPU nicht bekannt
//                return null;

//            if (parts[1].StartsWith("DB"))
//            {
//                if (IsAvailable(sps.IP))
//                    Console.WriteLine(cpuName + " ist erreichbar");

//                if (!sps.IsConnected)
//                {
//                    Console.WriteLine("Verbinde " + cpuName);

//                    sps.Open();
//                    Thread.Sleep(1000);
//                }

//                Console.WriteLine("Lese " + $"{parts[1]}.{parts[2]} an {cpuName}");
//                result = sps.Read($"{parts[1]}.{parts[2]}");
//            }
//            return result;
//        }


//        public static object? Read(string cpuName)
//        {
//            Sps[cpuName].Open();

//            return Sps[cpuName].Read("DB10.DBW6");
//        }

//        public static object? ReadSingle(string tagName)
//        {
//            object? result = null;

//            try
//            {
//                Console.WriteLine(tagName + " > ");
//                tagName = tagName.Replace("Mittel_", "").Replace("Dif_", "");

//                var parts = tagName.Split('_');

//                Plc plc = Sps[parts[0]];
//                if (plc == null)
//                    return null;


//                if (!plc.IsConnected)
//                    plc.Open();

//                if (parts[1].StartsWith("DB"))
//                {

//                    if (parts[2].StartsWith("DBX"))
//                        result = plc.Read($"{parts[1]}.{parts[2]}.{parts[3]}");
//                    else if (parts[2].StartsWith("DBD"))
//                    {
//                        //uint dword = (uint)plc.Read($"{parts[1]}.{parts[2]}");
//                        //double d = S7.Net.Types.Double.FromDWord(dword);

//                        //Console.WriteLine($"\r\n{tagName}={d} (dword {dword})\r\n");

//                        //int Db = int.Parse(parts[1].Substring(2));
//                        //int startAddr = int.Parse(parts[2].Substring(3));

//                        object? x = plc.Read($"{parts[1]}.{parts[2]}");
//                        _ = uint.TryParse(x?.ToString(), out uint dword);

////                        uint dword = (uint)plc.Read($"{parts[1]}.{parts[2]}");
//                        byte[] data = BitConverter.GetBytes(dword);
//                        var floatNumber = BitConverter.ToSingle(data, 0);

//                        //Log.Write(Log.Cat.InTouchVar, Log.Prio.Info, 22222, $"{tagName}= (dword){dword}\t(byte[]){data}\t(float){floatNumber}");

//                        result = floatNumber; // dword.ConvertToFloat();
//                    }
//                    else
//                        result = plc.Read($"{parts[1]}.{parts[2]}");
//                }
//                else
//                {
//                    result = plc.Read($"{parts[1]}.{parts[2]}.{parts[3]}");
//                }

//                //Log.Write(Log.Cat.InTouchVar, Log.Prio.LogAlways, 111111, $"{parts[1]}.{parts[2]}={result}");
//                Console.WriteLine($"{parts[1]}.{parts[2]}={result}");
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e);
//               // Log.Write(Log.Cat.InTouchVar, Log.Prio.Error, 00111, tagName + "\t" + e.GetType() + Environment.NewLine + e.Message);
//            }

//            return result;
//        }


//        public static void PlcRead(Plc plc, List<DataItem> tags, CancellationToken cancelToken, int readIntervall = 0)
//        {
//            new Thread(async () =>
//            {
//                plc.Open();

//                do
//                {
//                    if (cancelToken.IsCancellationRequested) //für dauerhafte Abfragen
//                        plc.Close();

//                    if (!plc.IsConnected) return;

//                    int index = 0;
//                    int end = tags.Count;
//                    while (index < end) //Es können max. 20 Tags in einer Abfrage sein
//                    {
//                        #region max. 20 Tags in einer Abfrage
//                        int count = Math.Min(end - index, 20);
//                        List<DataItem> range = tags.GetRange(index, count);
//                        index += count;
//                        #endregion

//                        _ = await plc.ReadMultipleVarsAsync(range, cancelToken);

//                        Console.WriteLine(Tags2Json(range)); // TODO: Das muss noch an REST-API geleitet werden.
//                    }

//                    System.Threading.Thread.Sleep(readIntervall); //für dauerhafte Abfragen

//                } while (readIntervall > 0);

//            }).Start();

//            plc.Close();
//        }

//        public static void CloseAllCpus()
//        {
//            foreach (var item in Sps)
//            {
//                item.Value.Close();
//            }
//        }

//        public static bool IsAvailable(string IP)
//        {
//            Ping ping = new Ping();
//            PingReply result = ping.Send(IP);
//            if (result.Status == IPStatus.Success)
//                return true;
//            else
//                return false;
//        }


//        public static List<DataItem> ItemNames2DataItems(List<string> itemNames)
//        {
//            List<DataItem> dataItems = new List<DataItem>();

//            foreach (var itemName in itemNames)
//                if (itemName.Length > 0)
//                    dataItems.Add(DataItem.FromAddress(itemName));

//            return dataItems;
//        }

//        public static string Tags2Json(List<DataItem> dataItems)
//        {
//            List<Tag> readTags = new List<Tag>();

//            foreach (var item in dataItems)
//            {
//                string itemName;
//                string offset = string.Empty;

//                switch (item.VarType)
//                {
//                    case VarType.Bit:
//                        offset = $"DBX{item.StartByteAdr}.{item.BitAdr}";
//                        break;
//                    case VarType.Byte:
//                        offset = $"DBB{item.StartByteAdr}";
//                        break;
//                    case VarType.Word:
//                    case VarType.Int:
//                        offset = $"DBW{item.StartByteAdr}";
//                        break;
//                    case VarType.DWord:
//                    case VarType.DInt:
//                        break;
//                    case VarType.Real:
//                        offset = $"DBD{item.StartByteAdr}";
//                        break;
//                        //case VarType.LReal:
//                        //    break;
//                        //case VarType.String:
//                        //    break;
//                        //case VarType.S7String:
//                        //    break;
//                        //case VarType.S7WString:
//                        //    break;
//                        //case VarType.Timer:
//                        //    break;
//                        //case VarType.Counter:
//                        //    break;
//                        //case VarType.DateTime:
//                        //    break;
//                        //case VarType.DateTimeLong:
//                        //    break;
//                        //default:
//                        //    break;
//                }

//                switch (item.DataType)
//                {
//                    case DataType.DataBlock:
//                        itemName = $"DB{item.DB}.{offset}";
//                        break;
//                    case DataType.Input:
//                        itemName = $"E{item.StartByteAdr}.{item.BitAdr}";
//                        break;
//                    case DataType.Output:
//                        itemName = $"A{item.StartByteAdr}.{item.BitAdr}";
//                        break;
//                    case DataType.Memory:
//                        itemName = $"M{item.StartByteAdr}.{item.BitAdr}";
//                        break;
//                    case DataType.Timer:
//                        itemName = "Timer";
//                        break;
//                    case DataType.Counter:
//                        itemName = "Counter";
//                        break;
//                    default:
//                        itemName = "Unbekannt";
//                        break;
//                }

//                if (itemName.Length > 0) // && item.Value.ToString().Length > 0)
//                    readTags.Add(new Tag(itemName, item.Value));
//            }

//            string json = System.Text.Json.JsonSerializer.Serialize(readTags) ?? "{}";

//            return json;
//        }


//        public static void PlcWriteOnceAsync(Plc plc, string tagName, string tagValue)
//        {
//            tagValue = tagValue.Trim();

//            Console.WriteLine($"Scheibe {tagName} = {tagValue}");
//            try
//            {
//                if (!plc.IsConnected)
//                {
//                    plc.Close();
//                    plc.Open();
//                }

//                if (!plc.IsConnected) return;

//                string[] tag = tagName.Split('.');

//                if (tag.Length == 1)
//                    return;
//                else if (tag.Length == 2)
//                {
//                    if (tag[1].StartsWith("DBD") && double.TryParse(tagValue, out double valReal)) //REAL
//                        plc.Write(tagName, valReal);
//                    else if (tag[1].StartsWith("DBW") && short.TryParse(tagValue, out short valShort)) //INT; WORD = ushort
//                        plc.Write(tagName, valShort);
//                    else if (tag[1].StartsWith("DINT") && int.TryParse(tagValue, out int valInt)) //DINT
//                        plc.Write(tagName, valInt);
//                }
//                else if (tag.Length > 2)
//                {
//                    if (tagValue == "true")
//                        plc.Write(tagName, true);
//                    else if (tagValue == "false")
//                        plc.Write(tagName, false);
//                }


//            }
//            catch (PlcException plc_ex)
//            {
//                Console.WriteLine(plc_ex.Message);
//                plc.Close();
//            }
//            catch (Exception ex)
//            {
//                plc.Close();
//                Console.WriteLine(ex.Message);
//                throw new Exception("PlcWriteOnceAsync() " + ex.Message);
//            }
//        }

//    }

//    public class Tag
//    {
//        public Tag (string name, object value)
//        {
//            _Name = name ?? string.Empty;
//            _Value = value ?? 0;
//        }

//        private string _Name;
//        private object _Value;

//        #region Basic Properties
//        public string Name { get => _Name; set => _Name = value ?? string.Empty; }
//        public object Value { get => _Value; set => _Value = value ?? 0; }
//        #endregion
//    }
//}


