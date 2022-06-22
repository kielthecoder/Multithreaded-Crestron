using System;
using Crestron.SimplSharp;

namespace ThreadTest
{
    public class TimerEventArgs : EventArgs
    {
        public string Message;

        public TimerEventArgs()
        {
            Message = "No message.";
        }

        public TimerEventArgs(string msg)
        {
            Message = msg;
        }
    }

    public class Test1
    {
        private CTimer _timer;
        private string _name;
        private long _frequency;
        private long _counter;
        private bool _running;

        public event EventHandler<TimerEventArgs> TimerEvent;

        public Test1()
        {
        }

        public void Initialize(string name, short frequency)
        {
            _name = name;
            _frequency = frequency;
            _timer = new CTimer(DoWork, this, Timeout.Infinite);
        }

        public void Start()
        {
            if (!_running)
            {
                _running = true;
                _counter = 0;
                _timer.Reset(_frequency, _frequency);
            }
        }

        public void Stop()
        {
            if (_running)
            {
                _running = false;
                _timer.Stop();
            }
        }

        private void DoWork(object userObj)
        {
            if (_running)
            {
                _counter++;

                if (TimerEvent != null)
                {
                    TimerEvent(userObj, new TimerEventArgs(String.Format("{0}: counting {1}", _name, _counter)));
                }
            }
        }
    }
}