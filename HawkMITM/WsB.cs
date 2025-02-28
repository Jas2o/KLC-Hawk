﻿using Fleck;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static LibKaseya.Enums;

namespace KLC_Hawk {
    public class WsB {

        private readonly LiveConnectSession Session;

        //WatsonWsServer is not used due to issues with messages arriving out of order
        private readonly WebSocketServer ServerB;
        private IWebSocketConnection ServerBsocket;
        public int PortB { get; private set; }
        private readonly WsY1 WebsocketY1;

        private VP8.Decoder decoder;
        private bool captureScreen;

        public WsB(LiveConnectSession session, WsY1 wsy1) {
            Session = session;
            WebsocketY1 = wsy1;

            //B - Find a free port me
            PortB = LiveConnectSession.GetNewPort();

            //B - new WebSocketServer (my port B)
            ServerB = new WebSocketServer("ws://0.0.0.0:" + PortB);

            ServerB.Start(socket => {
                ServerBsocket = socket;

                socket.OnOpen = () => {
                    ServerB_ClientConnected(socket);
                };
                socket.OnClose = () => {
                    ServerB_ClientDisconnected(socket);
                };
                socket.OnMessage = message => {
                    ServerB_MessageReceived(socket, message);
                };
                socket.OnPing = byteB => {
                    socket.SendPong(byteB);
                };
                socket.OnBinary = byteB => {
                    ServerB_BinaryReceived(socket, byteB);
                };
                socket.OnError = ex => {
                    //System.IO.File.WriteAllBytes("lastData.bin", lastData);

                    Console.WriteLine("B Error: " + ex.ToString());
                    Session.Parent.LogOld(Side.LiveConnect, PortB, "NA", "B Server Error: " + ex.ToString());
                };
            });
        }

        public void Stop()
        {
            if(ServerBsocket != null)
                ServerBsocket.Close();
            ServerB.ListenerSocket.Close();
        }

        private void ServerB_ClientConnected(IWebSocketConnection socket) {
            //Module = e.HttpRequest.Url.PathAndQuery.Split('/')[2];
            Session.Parent.LogText("B Connect (server port: " + PortB + ") " + socket.ConnectionInfo.Path);

            int clientPort = socket.ConnectionInfo.ClientPort;
            Session.Parent.LogOld(Side.AdminEndPoint, PortB, "B", "B Start on port " + PortB + " - " + clientPort + " - " + socket.ConnectionInfo.Path);

            if (socket.ConnectionInfo.Path == "/control/agent") {
                //Session.listY1Client.Last
                WsY1 client = Session.listY1Client.Find(x => x.Client == null);
                if (client == null)
                    throw new Exception();
                client.SetClient(socket);
                Session.Parent.LogOld(Side.AdminEndPoint, PortB, client.Module, "/control/agent");
                //Do nothing much
            } else {
                WsY2 client2 = new WsY2(Session, WebsocketY1.PortY, socket.ConnectionInfo.Path);
                client2.SetClient(this, socket);
                Session.listY2Client.Add(client2);
            }
        }

        private void ServerB_ClientDisconnected(IWebSocketConnection socket) {
            Session.Parent.LogText("B Close port " + PortB + " - " + socket.ConnectionInfo.Path);
            Session.Parent.LogOld(Side.MITM, PortB, "B", "B Server Close " + PortB + " - " + socket.ConnectionInfo.Path);

            if(socket.ConnectionInfo.Path == "/control/agent")
            {
                Session.Restart();
            }
        }

        private void ServerB_MessageReceived(IWebSocketConnection socket, string message) {
            //Session.Parent.LogText("B MSG " + e.IpPor);
            //if (eB.HttpRequest.Url.PathAndQuery == "/control/agent") {
            //!! -- Problem is it doesn't specify the websocketmessagetype

            if (socket.ConnectionInfo.Path == "/control/agent") {
                //Added because of RC Private new behavior
                WsY1 client = Session.listY1Client.Last();
                if (client == null)
                    throw new Exception();

                Session.Parent.LogOld(Side.AdminEndPoint, PortB, client.Module, message);
                client.Send(message);
            } else {
                WsY2 client2 = Session.listY2Client.Find(x => x.Client == socket);
                if (client2 == null) {
                    //Session.Parent.Log(Side.MITM, PortY, PortY, "Y2 needs to know B's client!");
                    bool saidWaiting = false;
                    while (client2 == null) {
                        if (!saidWaiting) {
                            Session.Parent.LogText("[!] B waiting");
                            saidWaiting = true;
                        }
                        Thread.Sleep(100);
                        client2 = Session.listY2Client.Find(x => x.Client == socket);
                    }
                    Session.Parent.LogText("B now knows!");
                }

                Session.Parent.LogOld(Side.AdminEndPoint, PortB, client2.Module, message);
                client2.Send(message);
            }
        }

        private void ServerB_BinaryReceived(IWebSocketConnection socket, byte[] data) {
            WsY2 client2 = Session.listY2Client.Find(x => x.Client == socket);
            if (client2 == null)
            {
                //Session.Parent.Log(Side.MITM, PortY, PortY, "Y2 needs to know B's client!");
                bool saidWaiting = false;
                while (client2 == null)
                {
                    if (!saidWaiting)
                    {
                        Session.Parent.LogText("[!] B waiting");
                        saidWaiting = true;
                    }
                    Thread.Sleep(100);
                    client2 = Session.listY2Client.Find(x => x.Client == socket);
                }
                Session.Parent.LogText("B now knows!");
            }

            bool doNothing = false;
            //if (client2 == null) {
                //doNothing = true;
            //}

            if (data.Length > 6 && data[5] == '{') {
                //Maybe some MITM?
                KaseyaMessageTypes kmtype = (KaseyaMessageTypes)data[0];
                byte[] bLen = new byte[4];
                Array.Copy(data, 1, bLen, 0, 4);
                Array.Reverse(bLen); //Endianness
                int jLen = BitConverter.ToInt32(bLen, 0);
                string message = Encoding.UTF8.GetString(data, 5, jLen);
                dynamic json = JsonConvert.DeserializeObject(message);

                int remStart = 5 + jLen;
                int remLength = data.Length - remStart;
                byte[] remaining = new byte[remLength];
                if (remLength > 0)
                    Array.Copy(data, remStart, remaining, 0, remLength);

                switch (kmtype) {
                    case KaseyaMessageTypes.Ping:
                    case KaseyaMessageTypes.CursorImage:
                        break;

                    case KaseyaMessageTypes.HostDesktopConfiguration:
                        //This is the dumbest/easiest way to tell if we're connected to a Mac to not use fast autotype.
                        string screen_name = (string)json["screens"][0]["screen_name"];
                        if (!screen_name.Contains("DISPLAY"))
                            Session.IsMac = true;
                        break;

                    case KaseyaMessageTypes.Mouse:
                        //This seems to be a 9.5.8007.17759 thing.
                        doNothing = true;
                        break;

                    case KaseyaMessageTypes.Video:
                        if(Session.Parent.EnablePrintScreenHack || captureScreen) {
                            if (decoder == null)
                                decoder = new VP8.Decoder();

                            Bitmap b1 = decoder.Decode(remaining);

                            if(captureScreen && b1 != null) {
                                captureScreen = false;
                                if (!Session.Parent.EnablePrintScreenHack)
                                    decoder = null;

                                //This may cause AV to think we're malware
                                Thread tc = new Thread(() => {
                                    Clipboard.SetImage(b1);
                                });
                                tc.SetApartmentState(ApartmentState.STA);
                                tc.Start();
                                Thread.Sleep(1);
                                tc.Join();
                                Thread.Sleep(1);

                                Session.Parent.LogText("Screenshot saved to clipboard", "screenshot");
                                b1.Dispose();
                            }
                        }

                        if (Session.Parent.EnableFrameAckHack)
                            Send(socket, MITM.GetFrameAcknowledgementMessage(json["sequence_number"], json["timestamp"]));
                        break;

                    case KaseyaMessageTypes.Clipboard:
                        if (socket.ConnectionInfo.Path == "/app/staticimage") {
                            //Thanks Kaseya
                            Session.Parent.LogText("StaticImage just got a clipboard event, dropping it.");
                            doNothing = true;
                        } else {
                            string clipboard = Encoding.UTF8.GetString(remaining); // Encoding.ASCII.GetString(e.Data, 5 + jLen, e.Data.Length - 5 - jLen);
                            Session.Parent.LogText("Clipboard receive: [" + clipboard + "]", "clipboard");

                            //if (!Session.Parent.EnableClipboardHostToRemote)
                            //doNothing = true;
                        }
                        break;

                    default:
                        //Session.Parent.LogText("B MSGb: " + kmtype.ToString());
                        break;
                }
            } //else Session.Parent.LogText("B MSGu");

            if (doNothing) {
            } else {
                Session.Parent.LogOld(Side.AdminEndPoint, PortB, client2.Module, data);
                client2.Send(data);
            }
        }

        //private byte[] lastData;
        //private string lastMessage;

        public void Send(IWebSocketConnection socket, byte[] data) {
            //Needed to slow it down for when Finch uses Hawk
            while (socket == null) {
                Task.Delay(10).Wait();
            }

            if (!socket.IsAvailable)
            {
                Session.Parent.LogText("WsB - SendB - Socket not available");
                return;
            }

            //lastData = data;

            socket.Send(data);
            return;
        }

        public void Send(IWebSocketConnection socket, string message) {
            //Needed to slow it down for when Finch uses Hawk
            while (socket == null) {
                Task.Delay(10).Wait();
            }

            if (!socket.IsAvailable) {
                Session.Parent.LogText("WsB - Send - Socket not available");
                return;
            }

            //lastMessage = message;

            socket.Send(message);
        }

        public void CaptureNextScreen() {
            captureScreen = true;
        }

    }
}
