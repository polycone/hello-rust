using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.ComponentModel;

namespace Udp
{
    public class Packet
    {
        public IPEndPoint FromEndPoint { set; get; }
        public IPEndPoint ToEndPoint { set; get; }
        public byte[] PacketData { set; get; }
        public int PacketSize { set; get; }
        public bool IsIncoming { set; get; }
    }

    public delegate void PacketHandler(Packet packet);

    public class UdpFilter
    {
        class NatEntryValue
        {
            public Socket RemoteSocket { set; get; }
            public EndPoint LocalEndPoint { set; get; }
            public byte[] Buffer { set; get; }
            public DateTime TimeStamp { set; get; }
        }

        IPEndPoint remoteEndPoint;
        public EndPoint RemoteEndPoint { get { return remoteEndPoint; } }
        public int NatLifeTime { get; set; }

        Socket udpListener;
        BackgroundWorker natCleanup;

        public event PacketHandler PacketFilter;

        const int SIO_UDP_CONNRESET = -1744830452;

        Dictionary<EndPoint, NatEntryValue> natTable;

        public UdpFilter(IPAddress remoteAddress, int remotePort, int localPort = -1, int bufferSize = 1500)
        {
            NatLifeTime = 20000;
            natTable = new Dictionary<EndPoint, NatEntryValue>();

            IPEndPoint listenEp = new IPEndPoint(IPAddress.Any, (localPort == -1) ? remotePort : localPort);
            udpListener = new Socket(listenEp.Address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            udpListener.IOControl(SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            udpListener.Bind(listenEp);

            remoteEndPoint = new IPEndPoint(remoteAddress, remotePort);

            natCleanup = new BackgroundWorker();
            natCleanup.DoWork += NatCleanup;
            natCleanup.WorkerSupportsCancellation = true;
            natCleanup.RunWorkerAsync();

            EndPoint nullEndPoint = new IPEndPoint(0, 0);
            byte[] buffer = new byte[bufferSize];
            udpListener.BeginReceiveFrom(buffer, 0, bufferSize, SocketFlags.None, ref nullEndPoint, new AsyncCallback(listenerReceive), buffer);
        }

        void NatCleanup(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            while (!worker.CancellationPending)
            {
                DateTime currentStamp = DateTime.UtcNow;
                lock (natTable)
                {
                    List<EndPoint> toRemove = new List<EndPoint>();
                    foreach (var entry in natTable)
                    {
                        if ((currentStamp - entry.Value.TimeStamp).TotalMilliseconds >= NatLifeTime)
                        {
                            toRemove.Add(entry.Key);
                        }
                    }
                    toRemove.ForEach(p => natTable.Remove(p));
                }
                Thread.Sleep(NatLifeTime / 2);
            }
        }

        void listenerReceive(IAsyncResult ar)
        {
            EndPoint recipientEndPoint = new IPEndPoint(0, 0);
            int size = udpListener.EndReceiveFrom(ar, ref recipientEndPoint);
            byte[] buffer = ar.AsyncState as byte[];
            if (size > 0)
            {
                NatEntryValue natEntry = null;
                lock (natTable)
                {
                    natTable.TryGetValue(recipientEndPoint, out natEntry);
                }
                if (natEntry == null)
                {
                    natEntry = new NatEntryValue
                    {
                        RemoteSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp),
                        TimeStamp = DateTime.UtcNow,
                        Buffer = new byte[buffer.Length],
                        LocalEndPoint = recipientEndPoint
                    };
                    natEntry.RemoteSocket.Connect(remoteEndPoint);
                    EndPoint nullEndPoint = new IPEndPoint(0, 0);
                    natEntry.RemoteSocket.BeginReceiveFrom(natEntry.Buffer, 0, natEntry.Buffer.Length, SocketFlags.None, ref nullEndPoint, new AsyncCallback(remoteReceive), natEntry);
                    lock (natTable)
                    {
                        natTable.Add(recipientEndPoint, natEntry);
                    }
                }
                natEntry.TimeStamp = DateTime.UtcNow;
                Packet packet = new Packet
                {
                    FromEndPoint = recipientEndPoint as IPEndPoint,
                    PacketData = buffer,
                    ToEndPoint = remoteEndPoint,
                    PacketSize = size,
                    IsIncoming = false
                };
                if (PacketFilter != null)
                    PacketFilter(packet);
                natEntry.RemoteSocket.SendTo(packet.PacketData, packet.PacketSize, SocketFlags.None, remoteEndPoint);
            }
            recipientEndPoint = new IPEndPoint(0, 0);
            udpListener.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref recipientEndPoint, new AsyncCallback(listenerReceive), ar.AsyncState);
        }

        void remoteReceive(IAsyncResult ar)
        {
            NatEntryValue natEntry = ar.AsyncState as NatEntryValue;
            EndPoint recipientEndPoint = new IPEndPoint(0, 0);
            int size = natEntry.RemoteSocket.EndReceiveFrom(ar, ref recipientEndPoint);
            if (size > 0)
            {
                Packet packet = new Packet
                {
                    FromEndPoint = remoteEndPoint,
                    PacketData = natEntry.Buffer,
                    ToEndPoint = natEntry.LocalEndPoint as IPEndPoint,
                    PacketSize = size,
                    IsIncoming = true
                };
                if (PacketFilter != null)
                    PacketFilter(packet);
                udpListener.SendTo(packet.PacketData, packet.PacketSize, SocketFlags.None, natEntry.LocalEndPoint);
            }
            if ((DateTime.UtcNow - natEntry.TimeStamp).TotalMilliseconds < NatLifeTime)
                natEntry.RemoteSocket.BeginReceiveFrom(natEntry.Buffer, 0, natEntry.Buffer.Length, SocketFlags.None, ref recipientEndPoint, new AsyncCallback(remoteReceive), ar.AsyncState);
        }

    }
}
