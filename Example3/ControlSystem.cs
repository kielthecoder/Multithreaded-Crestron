using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;

namespace Example3
{
    public class ControlSystem : CrestronControlSystem
    {
        private TCPServer _server;

        public ControlSystem() : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

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
                _server = new TCPServer("0.0.0.0", 9999, 1000, EthernetAdapterType.EthernetLANAdapter, 5);
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
                        _server.DisconnectAll();
                    }
                    break;
            }
        }
    }
}