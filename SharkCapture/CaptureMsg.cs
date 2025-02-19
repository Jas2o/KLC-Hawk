using LibKaseya;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using static LibKaseya.Enums;

namespace KLC_Hawk {
    public class CaptureMsg {

        public Side Side { get; private set; }
        public DateTime Timestamp { get; private set; }
        public int Port { get; private set; }
        public string Module { get; private set; }
        public Datatype Type { get; private set; }
        public int DataLength { get; private set; }
        public ArraySegment<byte> Data { get; private set; }

        public int Number { get; set; }
        public string Display { get; private set; } //Shown in table
        public string Text { get; private set; } //Shown in text box
        public Double Time { get; private set; }

        public bool FilterHideDefault { get; private set; }
        public string FilterReason { get; private set; }

        public CaptureMsg(FileStream fs, int number, DateTime timeCompare) {
            Number = number;
            byte[] datalen = new byte[4];

            Side = (Side)fs.ReadByte();

            byte[] dtb = new byte[8];
            fs.Read(dtb, 0, 8);
            Timestamp = DateTime.FromBinary(BitConverter.ToInt64(dtb, 0));

            Port = fs.ReadByte() + (fs.ReadByte() << 8);

            int moduleLen = fs.ReadByte();
            byte[] moduleb = new byte[moduleLen];
            fs.Read(moduleb, 0, moduleLen);
            Module = Encoding.UTF8.GetString(moduleb, 0, moduleLen);

            Type = (Datatype)fs.ReadByte();

            fs.Read(datalen, 0, 4);
            DataLength = BitConverter.ToInt32(datalen, 0);
            byte[] fsData = new byte[DataLength];
            fs.Read(fsData, 0, DataLength);
            Data = fsData;

            UpdateDisplayAndText();

            if (timeCompare == DateTime.MinValue)
                Time = 0.0;
            else
                Time = (Timestamp - timeCompare).TotalSeconds;
        }

        public CaptureMsg(int number, DateTime timeCompare, Side side, int port, string module, ArraySegment<byte> data) {
            Number = number;
            Side = side;
            Timestamp = DateTime.Now;
            Port = port;
            Module = module;
            Type = Datatype.Binary;
            Data = data;
            DataLength = Data.Count;

            UpdateDisplayAndText();

            if (timeCompare == DateTime.MinValue)
                Time = 0.0;
            else
                Time = (Timestamp - timeCompare).TotalSeconds;
        }

        public CaptureMsg(int number, DateTime timeCompare, Side side, int port, string module, string message) {
            Number = number;
            Side = side;
            Timestamp = DateTime.Now;
            Port = port;
            Module = module;
            Type = Datatype.String;
            Data = Encoding.UTF8.GetBytes(message);
            DataLength = Data.Count;

            UpdateDisplayAndText();

            if (timeCompare == DateTime.MinValue)
                Time = 0.0;
            else
                Time = (Timestamp - timeCompare).TotalSeconds;
        }

        public void UpdateDisplayAndText() {
            if (Type == Datatype.Binary) {
                if (Data.Count == 1)
                {
                    KaseyaMessageTypes kmtype = (KaseyaMessageTypes)Data[0];
                    Display += "Binary " + kmtype.ToString();
                }
                else
                {
                    Display = "Binary length " + DataLength;
                    if (Data.Count > 2 && Data[0] == '{' && Data[Data.Count - 1] == '}')
                    {
                        Type = Datatype.JSON;
                        Display = "b! JSON";
                        Text = Encoding.UTF8.GetString(Data);
                    }

                    if (Data.Count > 6 && Data[5] == '{')
                        Type = Datatype.bJSON;
                }
            } else if (Type == Datatype.String) {
                Display = Encoding.UTF8.GetString(Data);
                Text = Encoding.UTF8.GetString(Data);

                if (Display[0] == '{') {
                    Type = Datatype.JSON;
                    Display = "s! JSON";
                }
            } else if (Type == Datatype.JSON) {
                Display = "JSON";
            }

            if(Type == Datatype.bJSON) {
                Display = "b! bJSON";

                byte bCode = Data[0];
                byte[] bLen = new byte[4];
                Data.Slice(1, 4).CopyTo(bLen);
                Array.Reverse(bLen); //Endianness
                int jLen = BitConverter.ToInt32(bLen, 0);
                Text = Encoding.UTF8.GetString(Data.ToArray(), 5, jLen);
            }

            //-- Filtering
            if (Module == "dashboard")
            {
                FilterHideDefault = true;
                FilterReason = "Dashboard";
            }

            if (Type == Datatype.JSON) {
                Text = Encoding.UTF8.GetString(Data);

                dynamic json = JsonConvert.DeserializeObject(Text);
                Display += " " + (string)json["action"];
            } else if (Type == Datatype.bJSON) {
                KaseyaMessageTypes kmtype = (KaseyaMessageTypes)Data[0];
                Display += " " + kmtype.ToString();

                dynamic json = JsonConvert.DeserializeObject(Text);

                switch (kmtype) {
                    case KaseyaMessageTypes.Ping:
                    case KaseyaMessageTypes.FrameAcknowledgement:
                    case KaseyaMessageTypes.Video:
                    case KaseyaMessageTypes.CursorImage:
                    case KaseyaMessageTypes.ThumbnailResult:
                        FilterHideDefault = true;
                        FilterReason = kmtype.ToString();
                        break;

                    case KaseyaMessageTypes.Clipboard:
                        break;

                    case KaseyaMessageTypes.Mouse:
                        KaseyaMouseEventTypes kmet = (KaseyaMouseEventTypes)(int)json["type"];
                        if (kmet == KaseyaMouseEventTypes.Move)
                        {
                            FilterHideDefault = true;
                            FilterReason = "MouseMove";
                        }
                        Display += " " + kmet;
                        break;

                    case KaseyaMessageTypes.Keyboard:
                        KeycodeV2 key = KeycodeV2.List.Find(x => x.USBKeyCode == (int)json["usb_keycode"]);
                        Display += " " + ((bool)json["pressed"] ? "pressed" : "released");
                        if(key != null)
                            Display += " " + key.Display;
                        break;

                    default:
                        Display = kmtype.ToString();
                        break;
                }
            }

            //FilterHideDefault = false; //For testing
        }

        public byte[] ExportAsBytes() {
            byte[] dtb = BitConverter.GetBytes(Timestamp.ToBinary()); //8
            byte[] modb = Encoding.UTF8.GetBytes(Module);
            byte[] datalen = BitConverter.GetBytes(Data.Count);
            if (dtb.Length != 8 || datalen.Length != 4 || Module.Length > 100)
                throw new Exception();

            byte[] export = new byte[12 + modb.Length + 1 + datalen.Length + Data.Count];

            export[0] = (byte)Side;
            dtb.CopyTo(export, 1); //Length of 8
            export[9] = (byte)Port;
            export[10] = (byte)(Port >> 8);
            export[11] = (byte)Module.Length;

            int pos = 12;
            modb.CopyTo(export, pos);
            pos += modb.Length;

            export[pos] = (byte)Type;
            pos++;
            datalen.CopyTo(export, pos);
            pos += datalen.Length;
            Data.CopyTo(export, pos);

            return export;
        }
    }
}