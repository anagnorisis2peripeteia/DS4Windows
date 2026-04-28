/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Threading;
using DS4Windows;

namespace DS4WinWPF.DS4Control
{
    // Polls an OpenRGB source device on a background thread and exposes the latest
    // colour for DS4LightBar to apply to connected controllers.
    //
    // Usage:
    //   OpenRGBSyncService.Instance.Start("localhost", 6742, sourceDeviceIndex: 0);
    //   // later...
    //   OpenRGBSyncService.Instance.Stop();
    public class OpenRGBSyncService : IDisposable
    {
        private static readonly Lazy<OpenRGBSyncService> _instance =
            new Lazy<OpenRGBSyncService>(() => new OpenRGBSyncService());
        public static OpenRGBSyncService Instance => _instance.Value;

        private readonly OpenRGBClient client = new OpenRGBClient();

        private Thread pollThread;
        private volatile bool running;
        private const int POLL_INTERVAL_MS = 200;

        // The OpenRGB device index to read colour from (-1 = not configured)
        public int SourceDeviceIndex { get; private set; } = -1;

        // Cached colour from the last successful poll — shared across all DS4 slots
        private volatile DS4Color cachedColor = new DS4Color(0, 0, 255);
        private volatile bool hasCachedColor = false;

        // If true, DS4Windows also pushes its lightbar colour back to OpenRGB
        public bool PushEnabled { get; set; } = false;

        public bool IsRunning => running;

        private OpenRGBSyncService() { }

        public bool Start(string host, int port, int sourceDeviceIndex)
        {
            if (running) Stop();

            SourceDeviceIndex = sourceDeviceIndex;

            if (!client.TryConnect(host, port))
                return false;

            running = true;
            pollThread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "OpenRGBSyncPoll"
            };
            pollThread.Start();
            return true;
        }

        public void Stop()
        {
            running = false;
            pollThread?.Join(500);
            pollThread = null;
            hasCachedColor = false;
            client.Disconnect();
        }

        // Returns true and sets color if a valid cached colour is available.
        public bool TryGetColor(out DS4Color color)
        {
            if (!hasCachedColor || !running)
            {
                color = default;
                return false;
            }
            color = cachedColor;
            return true;
        }

        // Called by DS4LightBar when it has just set a lightbar colour, so we can
        // mirror it back to OpenRGB when push mode is enabled.
        public void PushColor(int deviceIndex, DS4Color color)
        {
            if (!PushEnabled || !running || deviceIndex < 0) return;
            try
            {
                var devInfo = client.GetControllerData(deviceIndex);
                if (devInfo == null) return;
                client.SetCustomMode(deviceIndex);
                client.UpdateAllLEDs(deviceIndex, devInfo.NumLEDs, color.red, color.green, color.blue);
            }
            catch { }
        }

        private void PollLoop()
        {
            // Retry connection if it drops
            while (running)
            {
                try
                {
                    if (!client.IsConnected)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (SourceDeviceIndex < 0)
                    {
                        Thread.Sleep(POLL_INTERVAL_MS);
                        continue;
                    }

                    OpenRGBDeviceInfo devInfo = client.GetControllerData(SourceDeviceIndex);
                    if (devInfo?.Colors != null && devInfo.Colors.Count > 0)
                    {
                        var (r, g, b) = devInfo.Colors[0];
                        cachedColor    = new DS4Color(r, g, b);
                        hasCachedColor = true;
                    }
                }
                catch { hasCachedColor = false; }

                Thread.Sleep(POLL_INTERVAL_MS);
            }
        }

        public void Dispose()
        {
            Stop();
            client.Dispose();
        }
    }
}
