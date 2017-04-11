using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Networking.Sockets;

namespace NewTelegramBot
{
    public sealed class NetworkHelper
    {
        public static IAsyncOperation<bool> IsServerAwake()
        {
            return IsServerTheAwake().AsAsyncOperation<bool>();
        }

        private static async Task<bool> IsServerTheAwake()
        {
            ResourceLoader config = ResourceLoader.GetForViewIndependentUse("Config");
            bool isAwake = false;
            StreamSocket socket = new StreamSocket();
            Windows.Networking.HostName serverHost = new Windows.Networking.HostName(config.GetString("NetworkHelper_HostName"));
            string serverPort = config.GetString("NetworkHelper_Port");
            try
            {
                await socket.ConnectAsync(serverHost, serverPort);
                isAwake = true;
                await socket.CancelIOAsync();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                string s = string.Empty;
            }
            catch (Exception ex)
            {
                string s = string.Empty;
            }
            finally
            {
                socket.Dispose();
            }
            return isAwake;
        }

        public static IAsyncAction WakeTheServer()
        {
            return WakeOnLan().AsAsyncAction();
        }

        private static async Task WakeOnLan()
        {
            byte[] mac = ResourceLoader.GetForViewIndependentUse("Config").GetString("NetworkHelper_HostMacAddress").Split(':').Select(x => Convert.ToByte(x, 16)).ToArray();

            // WOL packet contains a 6-bytes trailer and 16 times a 6-bytes sequence containing the MAC address.

            byte[] packet = new byte[17 * 6];

            // Trailer of 6 times 0xFF.
            for (int i = 0; i < 6; i++)
                packet[i] = 0xFF;

            // Body of magic packet contains 16 times the MAC address.
            for (int i = 1; i <= 16; i++)
                for (int j = 0; j < 6; j++)
                    packet[i * 6 + j] = mac[j];
            ArraySegment<byte> segmentPacket = new ArraySegment<byte>(packet);
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Broadcast, 40000);


            try
            {
                // WOL packet is sent over UDP 255.255.255.0:40000.
                //await client.Client.ConnectAsync(IPAddress.Parse("192.168.0.250"), 40000);
                using (UdpClient client = new UdpClient())
                {
                    client.EnableBroadcast = true;
                    // Send WOL packet.
                    await client.SendAsync(packet, packet.Length, ipEndPoint);
                }
            }
            catch (SocketException ex)
            {
                string s = ex.Message;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
            }
        }
    }
}


