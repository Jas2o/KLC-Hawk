using Fleck;
using LibKaseya;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KLC_Hawk {
    class MITM {

        //Templates for making your own Kaseya Live Connect messages!

        private static byte[] GetJsonMessage(Enums.KaseyaMessageTypes messageType, string json) {
            int jsonLen = json.Length;
            byte[] jsonBuffer = Encoding.UTF8.GetBytes(json);

            byte[] tosend = new byte[jsonLen + 5];
            tosend[0] = (byte)messageType;
            tosend[4] = (byte)jsonLen;
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

            return tosend;
        }

        public static byte[] GetFrameAcknowledgementMessage(dynamic sequence_number, dynamic timestamp) {
            //Need to verify the types, I believe int and long
            string sendjson = "{\"last_processed_sequence_number\":" + sequence_number + ",\"most_recent_timestamp\":" + timestamp + "}";
            return GetJsonMessage(Enums.KaseyaMessageTypes.FrameAcknowledgement, sendjson);
        }

        public static byte[] GetSendKey(KeycodeV2 keycode, bool pressed) {
            string sendjson = "{\"keyboard_layout_handle\":\"0\",\"keyboard_layout_local\":false,\"lock_states\":2,\"pressed\":" + pressed.ToString().ToLower() + ",\"usb_keycode\":" + keycode.USBKeyCode + ",\"virtual_key\":" + keycode.JavascriptKeyCode + "}";
            return GetJsonMessage(Enums.KaseyaMessageTypes.Keyboard, sendjson);
        }

        private static Thread threadSendText;
        public static void SendText(IWebSocketConnection socket, string text, int speedPreset = 0) {

            //Evil twins
            text = text.Replace('“', '"').Replace('”', '"').Replace('–', '-');

            threadSendText = new Thread(() => {
                //Fast
                int delayShift = 0;
                int delayKey = 0;

                if (speedPreset == 1) { //Average
                    delayShift = 25;
                    delayKey = 10;
                } else if (speedPreset == 2) { //Slow
                    delayShift = 75;
                    delayKey = 50;
                }

                //On a Windows computer the delays can be pretty short, however on a Mac even these long 75/50 delays tend to break.

                KeycodeV2 keyShift = KeycodeV2.List.Find(x => x.Key == Keys.ShiftKey);

                string lower = "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./";
                string upper = "~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?";

                bool shift = false;

                foreach (char c in text) {
                    if (upper.Contains(c) && !shift) {
                        shift = true;
                        socket.Send(MITM.GetSendKey(keyShift, true));
                        if (delayShift != 0)
                            Thread.Sleep(delayShift);
                    } else if (lower.Contains(c) && shift) {
                        shift = false;
                        socket.Send(MITM.GetSendKey(keyShift, false));
                        if (delayShift != 0)
                            Thread.Sleep(delayShift);
                    }

                    KeycodeV2 code = KeycodeV2.List.Find(x => x.Key == (Keys)(KeycodeV2.VkKeyScan(c) & 0xff));
                    if (code != null) {
                        socket.Send(MITM.GetSendKey(code, true));
                        if (delayKey != 0)
                            Thread.Sleep(delayKey);
                        socket.Send(MITM.GetSendKey(code, false));
                        if (delayKey != 0)
                            Thread.Sleep(delayKey);
                    }
                }

                if (shift) {
                    shift = false;
                    socket.Send(MITM.GetSendKey(keyShift, false));
                }
            });
            threadSendText.Start();
        }

        private const int KEYEVENTF_EXTENTEDKEY = 1;
        //private const int KEYEVENTF_KEYUP = 0;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);

        public static void HandleKey(KeycodeV2 keykaseyaUN) {
            keybd_event((byte)keykaseyaUN.JavascriptKeyCode, 0, KEYEVENTF_EXTENTEDKEY, IntPtr.Zero);
        }
    }
}
