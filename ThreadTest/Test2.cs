using System;
using Crestron.SimplSharp;

namespace ThreadTest
{
    public class Test2
    {
        private static CMutex _lock;
        private static int _counter;

        private string _name;
        private int _frequency;
        private bool _running;

        public event EventHandler<TimerEventArgs> TimerEvent;

        public Test2()
        {
            if (_lock == null)
            {
                _lock = new CMutex();
            }
        }

        public void Initialize(string name, short frequency)
        {
            _name = name;
            _frequency = (int)frequency;
        }

        public void Start()
        {
            if (!_running)
            {
                _running = true;
                CrestronInvoke.BeginInvoke(DoWork);
            }
        }

        public void Stop()
        {
            _running = false;
        }

        private void DoWork(object userObj)
        {
            while (_running)
            {
                CrestronEnvironment.Sleep(_frequency);

                if (_running)
                {
                    try
                    {
                        if (_lock.WaitForMutex(100))
                        {
                            _counter++;

                            if (TimerEvent != null)
                            {
                                TimerEvent(userObj,
                                    new TimerEventArgs(String.Format("{0}: counter = {1}",
                                        _name, _counter)));
                            }

                            _lock.ReleaseMutex();
                        }
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("Exception in {0}.DoWork: {1}",
                            _name,
                            e.Message);
                    }
                }
            }
        }
    }
}