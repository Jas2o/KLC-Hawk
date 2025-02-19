using Fleck;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static LibKaseya.Enums;

namespace KLC_Hawk {

    public class WsA {
        
        private LiveConnectSession Session;

        private WebSocketServer ServerA;
        private List<IWebSocketConnection> listServerAsocket;
        private IWebSocketConnection ServerAsocket; //last
        public int PortA { get; private set; }
        private string Module;

        public WsA(LiveConnectSession session) {
            Session = session;
            Module = "A";

            //A - Find a free port for me
            PortA = LiveConnectSession.GetNewPort();
            Session.Parent.LogOld(Side.LiveConnect, PortA, Module, "A Port");

            //A - new WebSocketServer (my port A)
            ServerA = new WebSocketServer("ws://0.0.0.0:" + PortA);
            listServerAsocket = new List<IWebSocketConnection>();

            //ServerA.RestartAfterListenError = true;
            ServerA.Start(socket => {
                ServerAsocket = socket;
                listServerAsocket.Add(socket);

                socket.OnOpen = () => {
                    Session.Parent.LogText("A Open (server port: " + PortA + ") " + socket.ConnectionInfo.Path);
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, "A Server opened"); //ServerOnOpen(socket);
                };
                socket.OnClose = () => {
                    Session.Parent.LogText("A Close");
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, "A Server close");

                    //Session.WebsocketZ.Stop();
                    //ServerA.ListenerSocket.Close();
                };
                socket.OnMessage = message => {
                    //Session.Parent.LogText("A Message to Z");
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, message);
                    //Parent.Log(PortA "Z", "Sent: " + message, PortA);

                    Session.WebsocketZ.Send(message);

                    if (message.Contains("PeerOffline"))
                    {
                        Session.Parent.LogText("A: Endpoint is offline.");

                        //Finch
                        //Session.Callback?.Invoke(EPStatus.PeerOffline);
                        //Task.Delay(10000).Wait(); // 10 seconds
                        //ServerOnOpen(socket);

                        //socket.Close();
                    } else if(message.Contains("PeerToPeerFailure"))
                    {
                        Session.Parent.LogText("A: PeerToPeerFailure");
                        //socket.Close();

                        //Finch
                        //Session.Callback?.Invoke(EPStatus.PeerToPeerFailure);
                        //Task.Delay(10000).Wait(); // 10 seconds
                        //ServerOnOpen(socket);
                    }
                    else {
                        Session.Parent.LogText("Unexpected A message: " + message);
                    }
                };
                socket.OnPing = byteA => {
                    //Session.Parent.LogText("A Ping");
                    //Required to stay connected longer than 2 min 10 sec
                    socket.SendPong(byteA);
                    //Session.Parent.LogOld(Side.LiveConnect, PortA, Module, "A Server Ping");
                };
                //socket.OnPong = byteA => Parent.LogAEP("Pong");
                socket.OnBinary = byteA => {
                    //Session.Parent.LogText("A Binary");
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, byteA);
                };
                socket.OnError = ex => {
                    Session.Parent.LogText("A Error: " + ex.ToString());
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, "A Server Error: " + ex.ToString());
                };
            });

            //A - Run AdminEndpoint (my port A)

            Process process = new Process();

            string[] files = new string[] {
                @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.org.exe",
                @"C:\Program Files\Kaseya Live Connect\Kaseya.AdminEndpoint.exe",
                Environment.ExpandEnvironmentVariables(@"%localappdata%\Apps\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.org.exe"),
                Environment.ExpandEnvironmentVariables(@"%localappdata%\Apps\Kaseya Live Connect\Kaseya.AdminEndpoint.exe")
            };

            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    process.StartInfo.FileName = file;
                    break;
                }
            }

            if (process.StartInfo.FileName.Length > 0)
            {
                process.StartInfo.Arguments = "-viewerport " + PortA;
                process.StartInfo.CreateNoWindow = true;
                //process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
            }

            //--

            //Y - When socket1 is told a module is connecting
            //socketN = new WebSocketClient(what LiveConnect said the module is)

            //new WebSocketServer
            //Tell AdminEndPoint
        }

        public void Stop()
        {
            foreach (IWebSocketConnection con in listServerAsocket)
                con.Close();

            //ServerAsocket.Close();
            //ServerA.ListenerSocket.Close();
        }

        public void Send(string message) {
            //Needed to slow it down for when Finch uses Hawk
            while (ServerAsocket == null) {
                Task.Delay(10).Wait();
            }

            if (!ServerAsocket.IsAvailable) {
                Session.Parent.LogText("WsA - Send - Socket not available");
                return;
            }

            try {
                Session.Parent.LogOld(Side.LiveConnect, PortA, Module, message);
                ServerAsocket.Send(message).Wait();
            } catch (Exception ex) {
                Session.Parent.LogText(ex.ToString());
            }
        }
    }
}
