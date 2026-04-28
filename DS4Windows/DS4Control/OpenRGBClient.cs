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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace DS4WinWPF.DS4Control
{
    public class OpenRGBDeviceInfo
    {
        public string Name { get; set; }
        public int Type { get; set; }
        public int NumLEDs { get; set; }
        public List<(byte R, byte G, byte B)> Colors { get; set; }
    }

    // Minimal OpenRGB SDK TCP client (protocol version 4).
    // Implements only what DS4Windows needs: enumerate devices and read/write LED colours.
    public class OpenRGBClient : IDisposable
    {
        private const int PROTOCOL_VERSION = 4;
        private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("ORGB");

        private TcpClient tcp;
        private NetworkStream stream;
        private readonly object syncLock = new object();

        public bool IsConnected
        {
            get { try { return tcp != null && tcp.Connected; } catch { return false; } }
        }

        public bool TryConnect(string host = "localhost", int port = 6742)
        {
            lock (syncLock)
            {
                try
                {
                    Disconnect();
                    tcp = new TcpClient();
                    tcp.Connect(host, port);
                    tcp.ReceiveTimeout = 2000;
                    stream = tcp.GetStream();
                    SendPacket(0, 50, Encoding.UTF8.GetBytes("DS4Windows\0")); // SET_CLIENT_NAME
                    return true;
                }
                catch
                {
                    Disconnect();
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            try { stream?.Close(); } catch { }
            try { tcp?.Close(); } catch { }
            stream = null;
            tcp = null;
        }

        public int GetControllerCount()
        {
            lock (syncLock)
            {
                try
                {
                    SendPacket(0, 0, null); // REQUEST_CONTROLLER_COUNT
                    byte[] data = ReceivePacketData();
                    return (int)BitConverter.ToUInt32(data, 0);
                }
                catch { return 0; }
            }
        }

        public OpenRGBDeviceInfo GetControllerData(int deviceIndex)
        {
            lock (syncLock)
            {
                try
                {
                    byte[] payload = BitConverter.GetBytes((uint)PROTOCOL_VERSION);
                    SendPacket((uint)deviceIndex, 1, payload); // REQUEST_CONTROLLER_DATA
                    byte[] data = ReceivePacketData();
                    return ParseDevice(data);
                }
                catch { return null; }
            }
        }

        // Put device into custom (static) mode so LED writes take effect immediately.
        public bool SetCustomMode(int deviceIndex)
        {
            lock (syncLock)
            {
                try
                {
                    SendPacket((uint)deviceIndex, 1100, null); // SETCUSTOMMODE
                    return true;
                }
                catch { return false; }
            }
        }

        // Push a single colour to all LEDs on a device.
        public bool UpdateAllLEDs(int deviceIndex, int ledCount, byte r, byte g, byte b)
        {
            if (ledCount <= 0) return false;
            lock (syncLock)
            {
                try
                {
                    // Payload: uint32 data_size, uint16 num_colors, per_color[R,G,B,alpha]
                    int numColors = ledCount;
                    int dataSize  = 2 + numColors * 4;
                    byte[] payload = new byte[4 + dataSize];
                    BitConverter.GetBytes((uint)dataSize).CopyTo(payload, 0);
                    BitConverter.GetBytes((ushort)numColors).CopyTo(payload, 4);
                    for (int i = 0; i < numColors; i++)
                    {
                        payload[6 + i * 4 + 0] = r;
                        payload[6 + i * 4 + 1] = g;
                        payload[6 + i * 4 + 2] = b;
                        payload[6 + i * 4 + 3] = 0;
                    }
                    SendPacket((uint)deviceIndex, 1050, payload); // UPDATELEDS
                    return true;
                }
                catch { return false; }
            }
        }

        private void SendPacket(uint deviceIdx, uint packetId, byte[] payload)
        {
            int payloadSize = payload?.Length ?? 0;
            byte[] header = new byte[16];
            MAGIC.CopyTo(header, 0);
            BitConverter.GetBytes(deviceIdx).CopyTo(header, 4);
            BitConverter.GetBytes(packetId).CopyTo(header, 8);
            BitConverter.GetBytes((uint)payloadSize).CopyTo(header, 12);
            stream.Write(header, 0, 16);
            if (payloadSize > 0)
                stream.Write(payload, 0, payloadSize);
        }

        private byte[] ReceivePacketData()
        {
            byte[] header   = ReadExact(16);
            uint   dataSize = BitConverter.ToUInt32(header, 12);
            return dataSize > 0 ? ReadExact((int)dataSize) : Array.Empty<byte>();
        }

        private byte[] ReadExact(int count)
        {
            byte[] buf  = new byte[count];
            int    read = 0;
            while (read < count)
            {
                int n = stream.Read(buf, read, count - read);
                if (n == 0) throw new Exception("OpenRGB connection closed");
                read += n;
            }
            return buf;
        }

        // --- Binary protocol parser ---

        private static OpenRGBDeviceInfo ParseDevice(byte[] data)
        {
            int pos = 0;
            OpenRGBDeviceInfo dev = new OpenRGBDeviceInfo();

            pos += 4; // total_size (uint32)

            dev.Type = (int)BitConverter.ToUInt32(data, pos);
            pos += 4;

            // Strings: name, vendor, description, version, serial, location
            dev.Name = ReadString(data, ref pos);
            for (int i = 0; i < 5; i++) SkipString(data, ref pos);

            pos += 4; // active_mode (int32)

            uint numModes = BitConverter.ToUInt32(data, pos); pos += 4;
            for (uint i = 0; i < numModes; i++) SkipMode(data, ref pos);

            uint numZones = BitConverter.ToUInt32(data, pos); pos += 4;
            for (uint i = 0; i < numZones; i++) SkipZone(data, ref pos);

            uint numLeds = BitConverter.ToUInt32(data, pos); pos += 4;
            dev.NumLEDs  = (int)numLeds;
            for (uint i = 0; i < numLeds; i++)
            {
                SkipString(data, ref pos); // LED name
                pos += 4;                  // LED value (uint32)
            }

            uint numColors = BitConverter.ToUInt32(data, pos); pos += 4;
            dev.Colors = new List<(byte R, byte G, byte B)>((int)numColors);
            for (uint i = 0; i < numColors; i++)
            {
                byte r = data[pos++];
                byte g = data[pos++];
                byte b = data[pos++];
                pos++;                     // alpha/speed byte
                dev.Colors.Add((r, g, b));
            }

            return dev;
        }

        private static string ReadString(byte[] data, ref int pos)
        {
            ushort len = BitConverter.ToUInt16(data, pos);
            pos += 2;
            if (len == 0) return string.Empty;
            string s = Encoding.UTF8.GetString(data, pos, len - 1); // len includes null terminator
            pos += len;
            return s;
        }

        private static void SkipString(byte[] data, ref int pos)
        {
            ushort len = BitConverter.ToUInt16(data, pos);
            pos += 2 + len;
        }

        private static void SkipMode(byte[] data, ref int pos)
        {
            SkipString(data, ref pos); // name
            pos += 4 * 11;             // value, flags, speed_min/max, brightness_min/max, colors_min/max, speed, brightness, direction, color_mode
            ushort numColors = BitConverter.ToUInt16(data, pos);
            pos += 2 + numColors * 4;
        }

        private static void SkipZone(byte[] data, ref int pos)
        {
            SkipString(data, ref pos); // name
            pos += 4 * 4;              // type, leds_min, leds_max, num_leds
            ushort matrixLen = BitConverter.ToUInt16(data, pos);
            pos += 2 + matrixLen * 4;  // matrix values (uint32 each)
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
