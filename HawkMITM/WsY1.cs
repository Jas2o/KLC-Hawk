using Fleck;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonWebsocket;
using static LibKaseya.Enums;

namespace KLC_Hawk {
    public class WsY1 {

        private LiveConnectSession Session;
        public int PortY { get; private set; }
        public string Module { get; private set; }

        private WatsonWsClient WebsocketY;
        private WsB WebsocketB;
        public IWebSocketConnection Client;
        public int ClientPort;

        public WsY1(LiveConnectSession session, int portY, bool halfMode = false) {
            //Type1 - is not started straight away
            Session = session;
            PortY = portY;
            Module = "controlagent";

            if (PortY == 0)
                throw new Exception("Port Y does not appear to be set.");

            if (halfMode)
                return;

            WebsocketY = new WatsonWsClient(new Uri("ws://127.0.0.1:" + PortY + "/control/agent"));

            WebsocketY.ServerConnected += WebsocketY1_ServerConnected;
            WebsocketY.ServerDisconnected += WebsocketY1_ServerDisconnected;
            WebsocketY.MessageReceived += WebsocketY1_MessageReceived;
            WebsocketY.Start();
        }

        private void WebsocketY1_ServerConnected(object sender, EventArgs e) {
            Session.Parent.LogText("Y1 Connected " + Module);
            Session.Parent.LogOld(Side.AdminEndPoint, PortY, Module, "Socket opened");
        }

        private void WebsocketY1_ServerDisconnected(object sender, EventArgs e) {
            Session.Parent.LogText("Y1 Disconnected " + Module);
            Session.Parent.LogOld(Side.AdminEndPoint, PortY, Module, "Socket closed");
        }

        private void WebsocketY1_MessageReceived(object sender, MessageReceivedEventArgs e) {
            if (Client == null) {
                Session.Parent.LogText("Y1 Needs to know B's client!");
                while (Client == null) {
                    Thread.Sleep(100);
                }
                Session.Parent.LogText("Y1 now knows!");
            }

            //string messageY = Encoding.UTF8.GetString(e.Data);
            //Session.Parent.Log(Side.AdminEndPoint, PortY, WebsocketB.PortB, e.Data);

            if (e.MessageType == System.Net.WebSockets.WebSocketMessageType.Text) {
                string messageY = Encoding.UTF8.GetString(e.Data);
                Client.Send(messageY);
            } else if (e.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
                Client.Send(e.Data);
        }

        public void SetClient(WsB wsB, IWebSocketConnection socket) {
            WebsocketB = wsB;
            Client = socket;
        }

        public void Send(byte[] data) {
            try {
                WebsocketY.SendAsync(data).Wait();
            } catch (Exception ex) {
                Session.Parent.LogText(ex.ToString());
            }
        }

        public void Send(string messageB) {
            try {
                WebsocketY.SendAsync(messageB).Wait();
            } catch (Exception ex) {
                Session.Parent.LogText(ex.ToString());
            }
        }
    }
}
