using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;

namespace Example3
{
    public class ControlSystem : CrestronControlSystem
    {
        public const int MAX_CONNECTIONS = 5;
        public const int INACTIVITY_DELAY_MS = 10000;

        private TCPServer _server;
        private Thread[] _timers;
        private int[] _lastActivity;

        public ControlSystem() : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 25;
                
                CrestronEnvironment.ProgramStatusEventHandler += HandleProgramStatus;
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
                _timers = new Thread[MAX_CONNECTIONS];
                _lastActivity = new int[MAX_CONNECTIONS];

                _server = new TCPServer("0.0.0.0", 9999, 1000,
                    EthernetAdapterType.EthernetLANAdapter, MAX_CONNECTIONS);

                _server.WaitForConnectionsAlways(ClientConnectionHandler);

                CrestronConsole.PrintLine("\nListening for connections on {0}:{1}",
                    _server.AddressToAcceptConnectionFrom, _server.PortNumber);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        private void HandleProgramStatus(eProgramStatusEventType status)
        {
            switch (status)
            {
                case (eProgramStatusEventType.Stopping):
                    if (_server != null)
                    {
                        CrestronConsole.PrintLine("Disconnecting all clients...");
                        _server.DisconnectAll();
                    }
                    break;
            }
        }

        private void ClientConnectionHandler(TCPServer srv, uint index)
        {
            if (index > 0)
            {
                if (srv.GetServerSocketStatusForSpecificClient(index) == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    CrestronConsole.PrintLine("Accepted connection from {0}, client index is {1}",
                        srv.GetAddressServerAcceptedConnectionFromForSpecificClient(index), index);

                    try
                    {
                        var msg = Encoding.ASCII.GetBytes(String.Format("Hello, you are client #{0}.  Type HELP if lost.\r\n", index));
                        srv.SendData(index, msg, msg.Length);

                        _lastActivity[index - 1] = CrestronEnvironment.TickCount;
                        _timers[index - 1] = new Thread(CheckForActivity, index);
                        
                        srv.ReceiveDataAsync(index, ClientDataReceive, 0, null);
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("Exception in ClientConnectionHandler: {0}", e.Message);
                    }
                }
                else
                {
                    CrestronConsole.PrintLine("Client #{0} status is {1}", index, srv.GetServerSocketStatusForSpecificClient(index));
                }
            }
        }

        private void ClientDataReceive(TCPServer srv, uint index, int bytesReceived, object userObj)
        {
            if (bytesReceived > 0)
            {
                try
                {
                    var data = srv.GetIncomingDataBufferForSpecificClient(index);
                    var str = Encoding.ASCII.GetString(data, 0, bytesReceived).Trim();

                    CrestronConsole.PrintLine("Received {0} bytes from client #{1}:", bytesReceived, index);
                    CrestronConsole.PrintLine("  {0}", str);

                    _lastActivity[index - 1] = CrestronEnvironment.TickCount;

                    var words = str.Split(' ');
                    var cmd = words[0].ToUpper();

                    if (cmd == "BYE")
                    {
                        var msg = Encoding.ASCII.GetBytes("Bye!\r\n");
                        srv.SendData(index, msg, msg.Length);

                        CrestronConsole.PrintLine("Disconnecting client #{0}...", index);
                        srv.Disconnect(index);
                    }
                    else
                    {
                        if (cmd == "HELP")
                        {
                            var help = Encoding.ASCII.GetBytes("List of commands I know:\r\n" +
                                "BYE   Disconnect from server\r\n" +
                                "HELP  Print this help message\r\n");
                            srv.SendData(index, help, help.Length);
                        }
                        else
                        {
                            if (cmd == "")
                            {
                                var hello = Encoding.ASCII.GetBytes("Hello???\r\n");
                                srv.SendData(index, hello, hello.Length);
                            }
                            else
                            {
                                var wah = Encoding.ASCII.GetBytes(String.Format("I don't know how to {0}!\r\n", cmd));
                                srv.SendData(index, wah, wah.Length);
                            }
                        }

                        var msg = Encoding.ASCII.GetBytes("\r\n>");
                        srv.SendData(index, msg, msg.Length);
                        srv.ReceiveDataAsync(index, ClientDataReceive, 0, userObj);
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Exception in ClientDataReceive: {0}", e.Message);
                }
            }
        }

        private object CheckForActivity(object userObj)
        {
            var index = (uint)userObj;

            CrestronConsole.PrintLine("Started inactivity thread for client #{0}...", index);

            while (_server.ClientConnected(index))
            {
                Thread.Sleep(INACTIVITY_DELAY_MS);

                if (_lastActivity[index - 1] + INACTIVITY_DELAY_MS < CrestronEnvironment.TickCount)
                {
                    var msg = Encoding.ASCII.GetBytes("\r\nGoodbye?\r\n");
                    _server.SendData(index, msg, msg.Length);
                    _server.Disconnect(index);
                    break;
                }
            }

            CrestronConsole.PrintLine("Leaving inactivity thread for client #{0}...", index);

            return null;
        }
    }
}