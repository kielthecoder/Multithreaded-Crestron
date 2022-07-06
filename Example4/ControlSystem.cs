using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;

namespace Example4
{
    public class ControlSystem : CrestronControlSystem
    {
        private TcpListener _server;
        private bool _listening;
        private List<TcpClient> _clients;

        public ControlSystem()
            : base()
        {
            try
            {
                CrestronEnvironment.ProgramStatusEventHandler += HandleProgramEvent;
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        public override void InitializeSystem()
        {
            try
            {
                // Start listening on port 9999
                _server = new TcpListener(System.Net.IPAddress.Parse("0.0.0.0"), 9999);
                _server.Start();

                // Keep track of all connected clients
                _clients = new List<TcpClient>();

                // Handle incoming connections on a different thread
                var t = new Thread(new ThreadStart(HandleConnections));
                t.Start();
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        void HandleProgramEvent(eProgramStatusEventType status)
        {
            if (status == eProgramStatusEventType.Stopping)
            {
                _listening = false;

                // Gracefully close all connected clients
                foreach (var c in _clients)
                {
                    c.Close();
                }

                // Stop the server
                _server.Stop();
            }
        }

        void HandleConnections()
        {
            _listening = true;

            CrestronConsole.PrintLine("\n\rListening for connections on {0}",
                _server.LocalEndpoint.ToString());

            while (_listening)
            {
                try
                {
                    // Check for pending connections and handle them
                    if (_server.Pending())
                    {
                        var client = _server.AcceptTcpClient();
                        _clients.Add(client);

                        // Pass client into new thread
                        var t = new Thread(new ParameterizedThreadStart(ClientSession));
                        t.Start(client);
                    }
                    else
                    {
                        // Have a snooze
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Exception in HandleConnections: {0}", e.Message);
                }
            }

            CrestronConsole.PrintLine("\n\rNo longer accepting connections on {0}",
                _server.LocalEndpoint.ToString());
        }

        void ClientSession(object userObj)
        {
            try
            {
                var client = (TcpClient)userObj;

                CrestronConsole.PrintLine("\n\rConnected!");
                
                using (var stream = client.GetStream())
                {
                    SendMessage(stream, "Hello! Type HELP if you are lost.");
                    SendMessage(stream, "\n\r> ", false);

                    // Set aside some memory for incoming data
                    var data = new byte[256];

                    while (client.Connected)
                    {
                        if (stream.DataAvailable)
                        {
                            // Read in available data
                            var length = stream.Read(data, 0, data.Length);

                            if (length > 0)
                            {
                                // Convert bytes to ASCII string and split on word boundaries
                                var text = Encoding.ASCII.GetString(data, 0, length).Trim();
                                var words = text.Split(' ');

                                if (text.Length < 3)
                                {
                                    CrestronConsole.Print("Received (hex): ");

                                    foreach (var c in text.ToCharArray())
                                    {
                                        CrestronConsole.Print(" {0:X} ", c);
                                    }

                                    CrestronConsole.PrintLine("");
                                }
                                else
                                {
                                    CrestronConsole.PrintLine("Received: \"{0}\"", text);
                                    CrestronConsole.Print("  ");

                                    foreach (var w in words)
                                    {
                                        CrestronConsole.Print(w.ToUpper() + " ");
                                    }

                                    CrestronConsole.PrintLine("({0})", words.Length);

                                    // Make sure we have at least 1 word
                                    if (words.Length > 0)
                                    {
                                        var cmd = words[0].ToUpper();

                                        if (cmd == "BYE")
                                        {
                                            SendMessage(stream, "Bye!");
                                            break;
                                        }
                                        else
                                        {
                                            if (cmd == "HELP")
                                            {
                                                SendMessage(stream, "List of commands that I know:");
                                                SendMessage(stream, "  BYE          - Disconnect from server");
                                                SendMessage(stream, "  HELLO [name] - Say hello to all other connected users");
                                                SendMessage(stream, "  HELP         - Print this help message");
                                            }
                                            else if (cmd == "HELLO")
                                            {
                                                foreach (var c in _clients)
                                                {
                                                    if (!c.Equals(client))
                                                    {
                                                        var otherStream = c.GetStream();
                                                        SendMessage(otherStream, String.Format("** Greetings from {0}! **", words[1]));
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                SendMessage(stream, "What are you yammering about?");
                                            }
                                        }
                                    }
                                }

                                // Ready for more input
                                SendMessage(stream, "\n\r> ", false);
                            }
                        }
                        else
                        {
                            // Have a snooze
                            Thread.Sleep(100);
                        }
                    }
                }

                if (client.Connected)
                {
                    client.Close();
                }

                _clients.Remove(client);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Exception in ClientSession: {0}", e.Message);
            }
        }

        void SendMessage(NetworkStream stream, string msg, bool newline=true)
        {
            var crlf = newline ? "\r\n" : "";
            var bytes = Encoding.ASCII.GetBytes(msg + crlf);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}