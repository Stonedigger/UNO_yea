﻿using Fleck;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using UNO.Model;

namespace UNO.Controller
{
    class UnoController
    {
        private static UnoController instance;

        public List<Spielfeld> tische { get; set; }
        

        public WebSocketServer websocketLobby { get; set; }

        
        public WebSocketServer websocketSpieler { get; set; }

        const int HttpPort = 1337;
        const int WebSocketPortSpieler = 666;
        const int WebSocketPortTisch = 667;
        const int WebSocketPortLobby = 668;
        string Ip = "127.0.0.1";
        List<ISpieler> AllSpieler = new List<ISpieler>();

        private UnoController()
        {
            tische = new List<Spielfeld>();
            Ip = GetLocalIPAddress();
            new SimpleHTTPServer("Web", HttpPort);

            websocketSpieler = new WebSocketServer($"ws://{Ip}:{WebSocketPortSpieler}");
            websocketSpieler.Start(socket => {
                socket.OnOpen = () => socket.Send("Spieler");
                socket.OnOpen = () => NewSpieler(socket);
            });

            websocketLobby = new WebSocketServer($"ws://{Ip}:{WebSocketPortLobby}");

            var obj = new { suc = true, type = "iniT", msg = "erstellt" };
            var json = new JavaScriptSerializer().Serialize(obj);

            websocketLobby.Start(socket => {
                socket.OnOpen = () => socket.Send(json);
                socket.OnOpen = () => NewSpielerLobby(socket);
            });

          

        }
        


        public static UnoController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new UnoController();
                }
                return instance;
            }
        }

        private void NewSpielerLobby(IWebSocketConnection socket)
        {
            socket.Send("connected");

            socket.OnMessage = (string message) => OnSend(message, socket);
        }


        private void NewSpieler(IWebSocketConnection socket)
        {
            Console.WriteLine("NEwSpielerSocket");
            var spielerName = "Spieler" + AllSpieler.Count;
            Spieler NewSpieler = new Spieler(spielerName, socket);
            NewSpieler.Socket.OnMessage = (string message) => NewSpieler.OnSend(message);
            var obj = new { suc = true, type = "spielerName", msg = spielerName };
            var json = new JavaScriptSerializer().Serialize(obj);
            socket.Send(json);
            AllSpieler.Add(NewSpieler);
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private void OnSend(string message, IWebSocketConnection socket)
        {
            try
            {
                JObject json = JObject.Parse(message);
                // Wir haben ein Json Object

                //var test = json.First;
                ISpieler currentSpieler = AllSpieler.Where(x => x.Name == (string) json.First.First).First();
                // Fall unterscheidung:
                if (message == "Ping")
                {
                    socket.Send("Pong");

                }
                else if (message.Contains("erstelleTisch"))
                {
                    //2 Tisch erstellen
                    SpielerTischErstellen(currentSpieler, socket);

                }
                else if (message.Contains("betritt-"))
                {
                    //3 Tisch beitretten

                }
                else if (message.Contains("exitTable-"))
                {
                    //4 Hat tisch verlassen
                    SpielerLeftTable(message, currentSpieler, socket);

                }
                else if (message == "GoodBye")
                {
                    //5 Ist gegagngen Spieler Löschen


                }
            } catch
            {
                // Dunno
            }
          
        }

        private void SpielerPing(ISpieler currentSpieler, IWebSocketConnection socket)
        {
            // Neuer Spieler:
            // KP ob was germacht werden muss
        }
        private void SpielerTischErstellen(ISpieler currentSpieler, IWebSocketConnection socket)
        {
            // Neues Spielfeld erstellen
            var spielerAmTisch = new List<ISpieler>();
            spielerAmTisch.Add(currentSpieler);
            tische.Add(new Spielfeld(spielerAmTisch));
            socket.Send("Tisch erstellt.");

        }
        private void SpielerTischBeitretten(string message, ISpieler currentSpieler, IWebSocketConnection socket)
        {
            // MEssage ist TischId
            //if(int.TryParse(message, out int tischId))
            int tischId = -1;
            if(int.TryParse(message, out tischId))
            {
                if (tischId >= 0 && tische.ElementAtOrDefault(tischId) != null)
                {
                    if(tische[tischId].IstOpen)
                    {
                        tische[tischId].AllSpieler.Add(currentSpieler);

                        var obj = new { suc = true};
                        var json = new JavaScriptSerializer().Serialize(obj);
                        socket.Send(json);
                    } else
                    {

                        var obj = new { suc = false, msg = "Tisch ist geschlossen!" };
                        var json = new JavaScriptSerializer().Serialize(obj);
                        socket.Send(json);
                    }
                } else
                {
                    var obj = new { suc = false, msg = "Invalide Tisch-Id" };
                    var json = new JavaScriptSerializer().Serialize(obj);
                    socket.Send(json);
                }

            } else
            {
                var obj = new { suc = false, msg = "Invalide Tisch-Id" };
                var json = new JavaScriptSerializer().Serialize(obj);
                socket.Send(json);
            }
        }

        private void SpielerLeftTable(string message, ISpieler currentSpieler, IWebSocketConnection socket)
        {
            // TODO
            //if(int.TryParse(message, out int tischId))
            int tischId = -1;
            if (int.TryParse(message, out tischId))
            {
                if (tischId >= 0 && tische.ElementAtOrDefault(tischId) != null)
                {
                    if (tische[tischId].IstOpen)
                    {
                        if (tische[tischId].AllSpieler.Contains(currentSpieler))
                        {
                            tische[tischId].RemoveSpieler(currentSpieler);
                        }

                    }
                    else
                    {

                        var obj = new { suc = false, msg = "Tisch ist geschlossen!" };
                        var json = new JavaScriptSerializer().Serialize(obj);
                        socket.Send(json);
                    }
                }
                else
                {
                    var obj = new { suc = false, msg = "Invalide Tisch-Id" };
                    var json = new JavaScriptSerializer().Serialize(obj);
                    socket.Send(json);
                }

            }
            else
            {
                var obj = new { suc = false, msg = "Invalide Tisch-Id" };
                var json = new JavaScriptSerializer().Serialize(obj);
                socket.Send(json);
            }
        }

        private void SpielerLeft(string message, ISpieler currentSpieler, IWebSocketConnection socket)
        {
            // TODO
        }
    }
}
