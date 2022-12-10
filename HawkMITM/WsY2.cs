using Fleck;
using LibKaseya;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WatsonWebsocket;
using static LibKaseya.Enums;

namespace KLC_Hawk {
    public class WsY2 {

        private readonly LiveConnectSession Session;
        public int PortY { get; private set; }
        public string Module { get; private set; }

        private readonly WatsonWsClient WebsocketY;
        private WsB WebsocketB;
        private bool hadStarted;
        public IWebSocketConnection Client;

        private readonly string PathAndQuery;

        //Need a better place for this as it's only used by the Remote Control module
        private readonly List<KeycodeV2> listHeldMods; //Modifier keys, they can stay down between any keys
        private readonly List<KeycodeV2> listHeldKeys; //Non-monifier keys, these should auto release any other non-modifier keys
        private bool autotypeAlwaysConfirmed;

        public WsY2(LiveConnectSession session, int portY, string PathAndQuery) {
            //Type 2 - is started
            Session = session;
            PortY = portY;
            this.PathAndQuery = PathAndQuery;
            Module = PathAndQuery.Split('/')[2];

            Session.Parent.LogText("New Y2 " + PathAndQuery);

            if (Module == "remotecontrol") {
                listHeldKeys = new List<KeycodeV2>();
                listHeldMods = new List<KeycodeV2>();
            }

            if (PortY == 0)
                throw new Exception("Port Y does not appear to be set.");

            WebsocketY = new WatsonWsClient(new Uri("ws://127.0.0.1:" + PortY + PathAndQuery + "?Y2"));

            WebsocketY.ServerConnected += WebsocketY2_ServerConnected;
            WebsocketY.ServerDisconnected += WebsocketY2_ServerDisconnected;
            WebsocketY.MessageReceived += WebsocketY2_MessageReceived;

            WebsocketY.Start();            
        }

        public void Stop()
        {
            WebsocketY.Stop();
        }

        private void WebsocketY2_ServerConnected(object sender, EventArgs e) {
            hadStarted = true;
            Session.Parent.LogText("Y2 Connect " + Module);
            Session.Parent.LogOld(Side.LiveConnect, PortY, Module, "Y2 Socket opened - " + PathAndQuery);
        }

        private void WebsocketY2_ServerDisconnected(object sender, EventArgs e) {
            Session.Parent.LogText("Y2 Disconnected " + Module);
            Session.Parent.LogOld(Side.LiveConnect, PortY, Module, "Y2 Socket closed");

            if (Module != "files")
                Client.Close();
        }

        private void WebsocketY2_MessageReceived(object sender, MessageReceivedEventArgs e) {
            //Session.Parent.LogText("Y2 Message");
            if (Client == null) {
                Session.Parent.LogText("Y2 Needs to know B's client!");
                //Session.Parent.Log(Side.MITM, PortY, PortY, "Y2 needs to know B's client!");
                while (Client == null) {
                    Thread.Sleep(100);
                }
                Session.Parent.LogText("Y2 now knows!");
            }

            bool doNothing = false;

            if (Module == "remotecontrol") {
                #region MITM
                if (e.Data.Length > 6 && e.Data[5] == '{') {
                    //Maybe some MITM?
                    KaseyaMessageTypes kmtype = (KaseyaMessageTypes)e.Data[0];
                    byte[] bLen = new byte[4];
                    Array.Copy(e.Data, 1, bLen, 0, 4);
                    Array.Reverse(bLen); //Endianness
                    int jLen = BitConverter.ToInt32(bLen, 0);
                    string message = Encoding.UTF8.GetString(e.Data, 5, jLen);
                    dynamic json;
                    try {
                        json = JsonConvert.DeserializeObject(message);
                    } catch(Exception ex) {
                        Session.Parent.LogText("============\r\nEXCEPTION Y2: " + ex.ToString() + "\r\n============\r\n" + message + "\r\n============\r\n" + BitConverter.ToString(e.Data).Replace("-", "") + "\r\n============");
                        return;
                    }

                    int remStart = 5 + jLen;
                    int remLength = e.Data.Length - remStart;
                    byte[] remaining = new byte[remLength];
                    if (remLength > 0)
                        Array.Copy(e.Data, remStart, remaining, 0, remLength);

                    switch (kmtype) {
                        case KaseyaMessageTypes.FrameAcknowledgement:
                            if (Session.Parent.EnableFrameAckHack)
                                doNothing = true;
                            break;

                        case KaseyaMessageTypes.Mouse:
                            #region Mouse - Middle click to auto type up to 50 characters from the clipboard
                            KaseyaMouseEventTypes kmet = (KaseyaMouseEventTypes)(int)json["type"];
                            if (kmet == KaseyaMouseEventTypes.Button && (int)json["button"] == 2) { //Middle mouse button
                                if (Session.Parent.EnableAutoType) {
                                    doNothing = true;
                                    if ((bool)json["button_down"]) {

                                        //Test
                                        //Session.Parent.ActivateOverlay(WebsocketB, Client);

                                        string text = "";

                                        //This may cause AV to think we're malware
                                        Thread tc = new Thread(() => text = Clipboard.GetText().Trim());
                                        tc.SetApartmentState(ApartmentState.STA);
                                        tc.Start();
                                        Thread.Sleep(1);
                                        tc.Join();
                                        Thread.Sleep(1);

                                        bool confirmed = false;
                                        if (!text.Contains('\n') && !text.Contains('\r')) {
                                            if (text.Length < 51 || autotypeAlwaysConfirmed) {
                                                confirmed = true;
                                            } else {
                                                tc = new Thread(() => {
                                                    WindowConfirmation winConfirm = new WindowConfirmation("You really want to autotype this?", text) {
                                                        Topmost = true
                                                    };
                                                    confirmed = (bool)winConfirm.ShowDialog();
                                                    if (confirmed && (bool)winConfirm.chkDoNotAsk.IsChecked)
                                                        autotypeAlwaysConfirmed = true;
                                                });
                                                tc.SetApartmentState(ApartmentState.STA);
                                                tc.Start();
                                                tc.Join();
                                            }
                                        }

                                        if (confirmed) {
                                            Session.Parent.LogText("Attempt autotype of " + text, "autotype");

                                            int speedPreset = Session.Parent.AutoTypeSpeed;
                                            if (Session.IsMac)
                                                speedPreset = 2;
                                            MITM.SendText(Client, text, speedPreset);

                                            //Session.Parent.Log(Side.MITM, PortY, PortY, "Send keys: " + text);
                                        } else
                                            Session.Parent.LogText("Autotype blocked: too long or had a new line character");
                                    }
                                }
                            }
                            #endregion
                            break;

                        case KaseyaMessageTypes.Keyboard:
                            #region Keyboard key press release order fix (but not Macs)
                            //The goal of this MITM fix is to prevent keys from being sent in the wrong order by releasing them earlier than as said by Live Connect!

                            if (Session.Parent.EnableKeyboardReleaseHack) {

                                //Hasn't been updated to KeycodeV3

                                try
                                {
                                    //Using the USB keycode is unreliable
                                    KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.USBKeyCode == (int)json["usb_keycode"]);
                                    if (keykaseya == null) {
                                        doNothing = true;

                                        KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.USBKeyCode == (int)json["usb_keycode"]);
                                        if (keykaseyaUN == null)
                                            Session.Parent.LogText("Unknown key (USB): " + json["usb_keycode"]);
                                        else if (keykaseyaUN.Key == Keys.PrintScreen) {
                                            if (!(bool)json["pressed"])
                                                WebsocketB.CaptureNextScreen();
                                        } else if (keykaseyaUN.Key == Keys.Pause) {
                                            if (!(bool)json["pressed"]) {
                                                Session.Parent.LogText("MITM release all modifiers", "keyrelease");
                                                foreach (int jskey in KeycodeV2.ModifiersJS) {
                                                    KeycodeV2 key = KeycodeV2.List.Find(x => x.JavascriptKeyCode == jskey);
                                                    WebsocketB.Send(Client, MITM.GetSendKey(key, false));
                                                }
                                            }
                                        } else {
                                            if ((bool)json["pressed"]) {
                                                Session.Parent.ActivateWindow();
                                                MITM.HandleKey(keykaseyaUN);
                                            }
                                        }
                                    } else {

                                        bool keyIsMod = KeycodeV2.ModifiersJS.Contains(keykaseya.JavascriptKeyCode);

                                        if ((bool)json["pressed"]) {
                                            if (keyIsMod) {
                                                if (!listHeldMods.Contains(keykaseya))
                                                    listHeldMods.Add(keykaseya);
                                                else
                                                    doNothing = true;
                                            } else {
                                                //doMITM = true;

                                                foreach (KeycodeV2 held in listHeldKeys) {
                                                    if (held == keykaseya)
                                                        continue;

                                                    string sendjson = "{\"keyboard_layout_handle\":\"0\",\"keyboard_layout_local\":false,\"lock_states\":2,\"pressed\":false,\"usb_keycode\":" + held.USBKeyCode + ",\"virtual_key\":" + held.JavascriptKeyCode + "}";
                                                    byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
                                                    int jsonLen = jsonBuffer.Length;

                                                    byte[] tosend = new byte[jsonLen + 5];
                                                    tosend[0] = (byte)KaseyaMessageTypes.Keyboard;
                                                    tosend[4] = (byte)jsonLen;
                                                    Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

                                                    Session.Parent.LogText("MITM release key: " + held.Display, "keyrelease");
                                                    WebsocketB.Send(Client, tosend);
                                                    //Session.Parent.LogOld(Side.MITM, PortY, PortY, tosend);
                                                }
                                                listHeldKeys.Clear();

                                                listHeldKeys.Add(keykaseya);
                                            }
                                        } else {
                                            if (keyIsMod)
                                                listHeldMods.Remove(keykaseya);
                                            else
                                                listHeldKeys.Remove(keykaseya);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }
                            } //End key release hack
                              //End keyboard message
                            #endregion
                            break;

                        case KaseyaMessageTypes.Clipboard:
                            string clipboard = Encoding.UTF8.GetString(remaining);

                            if (clipboard.Length == 0)
                                doNothing = true;
                            else {
                                Session.Parent.LogText("Clipboard send: [" + clipboard + "]", "clipboard");
                                if (!Session.Parent.EnableClipboardHostToRemote)
                                    doNothing = true;
                            }
                            break;
                    }
                }
                #endregion
            }

            if (doNothing) {
                //Session.Parent.Log(Side.MITM, PortY, WebsocketB.PortB, "???");
            } else {
                if (e.Data.Length == 0)
                    return; //This happens when closing remote control

                Session.Parent.LogOld(Side.LiveConnect, PortY, Module, e.Data); //Slow

                string messageY = Encoding.UTF8.GetString(e.Data);
                if (messageY[0] == '{')
                    WebsocketB.Send(Client, messageY);
                //Session.ServerB.SendAsync(Client, messageY);
                else
                    WebsocketB.Send(Client, e.Data);
                //Session.ServerB.SendAsync(Client, e.Data);
            }
        }

        public void SetClient(WsB wsB, IWebSocketConnection socket) {
            WebsocketB = wsB;
            Client = socket;
        }

        public void Send(byte[] data) {
            while (!hadStarted) {
                Session.Parent.LogText("[!] Y2 " + Module + " waiting");
                Thread.Sleep(100);
            }

            //if (!WebsocketY.Connected)
                //return;

            try {
                WebsocketY.SendAsync(data).Wait();
            } catch (Exception ex) {
                Session.Parent.LogText(ex.ToString());
            }
        }

        public void Send(string messageB) {
            while(!hadStarted) {
                Session.Parent.LogText("[!] Y2 " + Module + " waiting");
                Thread.Sleep(100);
            }

            //if (!WebsocketY.Connected)
                //return;

            try {
                WebsocketY.SendAsync(messageB).Wait();
            } catch (Exception ex) {
                Session.Parent.LogText(ex.ToString());
            }
        }
    }
}
