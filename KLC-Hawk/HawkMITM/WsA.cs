﻿using Fleck;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static LibKaseya.Enums;

namespace KLC_Hawk {

    public class WsA {
        
        private LiveConnectSession Session;

        private WebSocketServer ServerA;
        private IWebSocketConnection ServerAsocket;
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

            //ServerA.RestartAfterListenError = true;
            ServerA.Start(socket => {
                ServerAsocket = socket;

                socket.OnOpen = () => {
                    Session.Parent.LogText("A Open (server port: " + PortA + ") " + socket.ConnectionInfo.Path);
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, "A Server opened"); //ServerOnOpen(socket);
                };
                socket.OnClose = () => {
                    Session.Parent.LogText("A Close");
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, "A Server close");
                };
                socket.OnMessage = message => {
                    //Session.Parent.LogText("A Message to Z");
                    Session.Parent.LogOld(Side.LiveConnect, PortA, Module, message);

                    Session.WebsocketZ.Send(message);
                    //Parent.Log(PortA "Z", "Sent: " + message, PortA);
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
            string file1 = @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.org.exe";
            string file2 = @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.exe";
            Process process = new Process();
            process.StartInfo.FileName = (File.Exists(file1) ? file1 : file2);
            process.StartInfo.Arguments = "-viewerport " + PortA;
            process.Start();

            //--

            //Y - When socket1 is told a module is connecting
            //socketN = new WebSocketClient(what LiveConnect said the module is)

            //new WebSocketServer
            //Tell AdminEndPoint
        }

        public void Send(string message) {
            try {
                ServerAsocket.Send(message).Wait();

                Session.Parent.LogOld(Side.LiveConnect, PortA, Module, message);
            } catch (Exception ex) {
                Session.Parent.LogText(ex.ToString());
            }
        }
    }
}
