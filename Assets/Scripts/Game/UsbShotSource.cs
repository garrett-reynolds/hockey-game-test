using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using HockeyGame.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HockeyGame
{
    /// <summary>
    /// Reads real Shots from the tracking electronics over USB serial (115200 baud, lines end in \n).
    /// One background thread finds the port, loops on ReadLine() with a 500 ms timeout and queues Shots;
    /// the main thread takes them with <see cref="TryTakeShot"/>. If the port is lost the thread closes it
    /// and keeps retrying by itself. DataReceived is not used: it never fires in Unity's Mono.
    /// Port choice comes from SaveStore.Current.UsbPort (Auto or a port name) and is re-read every frame,
    /// so a settings change reconnects. In Auto mode the port that answered is saved as LastUsbPort.
    /// </summary>
    public sealed class UsbShotSource : MonoBehaviour
    {
        const int BaudRate = 115200;
        const int ReadTimeoutMs = 500;
        const int ProbeMs = 3000;      // per port in Auto mode: wait this long for READY (boards reset on open)
        const int RetryMs = 2000;      // wait between connection attempts
        const int PortCheckMs = 2000;  // while quiet, check this often that the port is still listed
        const int StopJoinMs = 2000;

        readonly ConcurrentQueue<UsbLine> _shots = new ConcurrentQueue<UsbLine>();
        readonly AutoResetEvent _wake = new AutoResetEvent(false);
        readonly object _portLock = new object();

        Thread _thread;
        SerialPort _port;
        volatile bool _running;
        volatile bool _restart;
        volatile string _setting;
        volatile string _remembered;
        volatile string _connectedPort;
        string _foundAutoPort; // set by the thread, taken by Update with Interlocked

        /// <summary>The open port's name, or null when no electronics are connected.</summary>
        public string ConnectedPort => _connectedPort;

        /// <summary>"Connected (COM3)" or "No electronics found".</summary>
        public string StatusText => UsbPorts.StatusText(_connectedPort);

        /// <summary>Takes the next real Shot read from the electronics, if any. Main thread.</summary>
        public bool TryTakeShot(out UsbLine shot)
        {
            return _shots.TryDequeue(out shot);
        }

        void OnEnable()
        {
            var save = SaveStore.Current;
            _setting = save.UsbPort;
            _remembered = save.LastUsbPort;
            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "USB electronics" };
            _thread.Start();
        }

        void OnDisable()
        {
            _running = false;
            _wake.Set();
            ClosePort(); // unblocks a ReadLine in progress
            if (_thread != null && !_thread.Join(StopJoinMs))
                Debug.LogWarning("USB read thread did not stop in time.");
            _thread = null;
            _connectedPort = null;
        }

        void Update()
        {
            var save = SaveStore.Current;
            if (save.UsbPort != _setting)
            {
                _setting = save.UsbPort;
                _restart = true;
                _wake.Set();
            }

            var found = Interlocked.Exchange(ref _foundAutoPort, null);
            if (found != null && save.LastUsbPort != found)
            {
                save.LastUsbPort = found;
                SaveStore.Save();
            }
        }

        // ---- background thread: no Unity API calls below except Debug.Log ----

        void Run()
        {
            while (_running)
            {
                _restart = false;
                var port = Connect();
                if (port != null)
                {
                    ReadUntilLost(port);
                    ClosePort();
                    _connectedPort = null;
                    if (_restart) continue;
                }
                _wake.WaitOne(RetryMs);
            }
        }

        SerialPort Connect()
        {
            string setting = _setting;
            bool auto = UsbPorts.IsAuto(setting);
            UsbPorts.TryList(out var names);

            foreach (var name in UsbPorts.ToTry(setting, _remembered, names))
            {
                if (!_running || _restart) return null;
                var port = TryOpen(name);
                if (port == null) continue;
                if (!auto || Probe(port))
                {
                    _connectedPort = name;
                    if (auto)
                    {
                        _remembered = name;
                        Interlocked.Exchange(ref _foundAutoPort, name);
                    }
                    return port;
                }
                ClosePort();
            }
            return null;
        }

        SerialPort TryOpen(string name)
        {
            var port = new SerialPort(name, BaudRate)
            {
                NewLine = "\n",
                ReadTimeout = ReadTimeoutMs,
                WriteTimeout = ReadTimeoutMs,
                DtrEnable = true, // some boards only send once DTR is set
            };
            try
            {
                port.Open();
            }
            catch (Exception)
            {
                try { port.Dispose(); } catch (Exception) { }
                return null;
            }
            lock (_portLock) _port = port;
            if (!_running) { ClosePort(); return null; }
            return port;
        }

        /// <summary>Auto mode: the electronics answer with READY (sent on power-up). A real Shot also counts.</summary>
        bool Probe(SerialPort port)
        {
            var clock = Stopwatch.StartNew();
            while (_running && !_restart && clock.ElapsedMilliseconds < ProbeMs)
            {
                try
                {
                    var line = UsbLine.Read(port.ReadLine());
                    if (line.Kind == UsbLineKind.Ready) return true;
                    if (line.Kind == UsbLineKind.Shot)
                    {
                        _shots.Enqueue(line);
                        return true;
                    }
                }
                catch (TimeoutException) { }
                catch (Exception) { return false; }
            }
            return false;
        }

        void ReadUntilLost(SerialPort port)
        {
            string name = port.PortName;
            var sinceCheck = Stopwatch.StartNew();
            while (_running && !_restart)
            {
                try
                {
                    var line = UsbLine.Read(port.ReadLine());
                    if (line.Kind == UsbLineKind.Shot) _shots.Enqueue(line);
                }
                catch (TimeoutException)
                {
                    // A pulled cable may only show as endless timeouts, so check the port is still there.
                    if (sinceCheck.ElapsedMilliseconds >= PortCheckMs)
                    {
                        sinceCheck.Restart();
                        if (!IsListed(name)) return;
                    }
                }
                catch (Exception)
                {
                    return; // IOException, closed port, etc.: the port is lost
                }
            }
        }

        static bool IsListed(string name)
        {
            if (!UsbPorts.TryList(out var names)) return true; // can't tell: keep reading
            return Array.IndexOf(names, name) >= 0;
        }

        void ClosePort()
        {
            SerialPort port;
            lock (_portLock)
            {
                port = _port;
                _port = null;
            }
            if (port == null) return;
            try { port.Close(); } catch (Exception) { }
            try { port.Dispose(); } catch (Exception) { }
        }
    }
}
