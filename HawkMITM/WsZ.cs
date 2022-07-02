using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WatsonWebsocket;
using static LibKaseya.Enums;

namespace KLC_Hawk {
    public class WsZ {

        private readonly LiveConnectSession Session;
        private readonly WatsonWsClient WebsocketZ;
        
        public int PortZ { get; private set; }
        private readonly string Module;
        private readonly bool HalfMode;

        public WsZ(LiveConnectSession session, int port, bool halfMode = false) {
            Session = session;
            PortZ = port;
            Module = "controladmin";
            HalfMode = halfMode;

            //LC Z - Live Connect found a free port
            Session.Parent.LogOld(Side.LiveConnect, PortZ, Module, "Z Received connection");

            //MM Z - We start a WebSocketClient and connect to Z
            WebsocketZ = new WatsonWsClient(new Uri("ws://127.0.0.1:" + PortZ + "/control/admin"));
            //For reasons I do not understand, if you use 'localhost' instead of '127.0.0.1', you'll miss out on the first message.

            WebsocketZ.ServerConnected += WebsocketZ_ServerConnected;
            WebsocketZ.ServerDisconnected += WebsocketZ_ServerDisconnected;
            WebsocketZ.MessageReceived += WebsocketZ_MessageReceived;

            WebsocketZ.Start();
        }

        private void WebsocketZ_ServerConnected(object sender, EventArgs e) {
            Session.Parent.LogText("Z Connect (server port: " + PortZ + ") /control/admin");
            Session.Parent.LogOld(Side.LiveConnect, PortZ, Module, "Z Socket opened: /control/admin");

            Session.WebsocketA = new WsA(Session);
        }

        public void Stop()
        {
            WebsocketZ.Stop();
        }

        public void Send(string message) {
            if (!WebsocketZ.Connected)
                return;

            try {
                WebsocketZ.SendAsync(message).Wait();
            } catch(Exception ex) {
                Session.Parent.LogText(ex.ToString());
            }
        }

        private void WebsocketZ_ServerDisconnected(object sender, EventArgs e) {
            Session.Parent.LogOld(Side.LiveConnect, PortZ, Module, "Z Socket disconnect");
        }

        private void WebsocketZ_MessageReceived(object sender, MessageReceivedEventArgs e) {
            if (e.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) {
                Session.Parent.LogText("Z received Close");
                return;
            }

            Session.Parent.LogOld(Side.LiveConnect, PortZ, Module, e.Data);

            //Z - Tells us who it is, and to start a module on port Y
            string message = Encoding.UTF8.GetString(e.Data);
            dynamic json = JsonConvert.DeserializeObject(message);
            if (json["data"]["connectPort"] != null) {
                Session.Parent.LogText("Z connectPort");

                int portY = (int)json["data"]["connectPort"];

                //Session.PortY = json["data"]["connectPort"];
                Session.Parent.LogOld(Side.MITM, portY, Module, "Y Port");

                WsY1 wsy1 = new WsY1(Session, portY, HalfMode);
                Session.listY1Client.Add(wsy1);

                if (!HalfMode) {
                    WsB wsb = new WsB(Session, wsy1); //This creates Y2
                    Session.listBsocket.Add(wsb);

                    json["data"]["connectPort"] = wsb.PortB; //Doesn't work with a string

                    Session.Parent.LogOld(Side.MITM, 0, Module, string.Format("!#!#!#!#! Replaced Y:{0} with B:{1}", portY, wsb.PortB)); //"MITM"
                    Session.Parent.LogText(string.Format("!#!#!#!#! Replaced Y:{0} with B:{1}", portY, wsb.PortB));
                }

                string newMessage = JsonConvert.SerializeObject(json);

                Session.WebsocketA.Send(newMessage);

                Session.Parent.LogOld(Side.LiveConnect, PortZ, Module, newMessage);
                //Parent.Log(Side.MITM, 0, 0, seq + " Z to A");
            } else {
                Session.Parent.LogText("Z Else");

                while(Session.WebsocketA == null) {
                    Task.Delay(10).Wait();
                }

                Session.WebsocketA.Send(message);
            }
            //Y - Tells us more about the module's demands (e.g. remote control)
        }

    }
}
