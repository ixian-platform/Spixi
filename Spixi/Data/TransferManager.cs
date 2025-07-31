using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using SPIXI.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SPIXI
{
    class FileTransfer
    {
        public string uid = null;
        public string fileName = null;
        public ulong fileSize = 0;
        public byte[] preview = null;       // Additional preview data
        public Address sender = null;        // Additional sender address field
        public int packetSize = Config.packetDataSize;

        public bool incoming = false;       // Incoming or outgoing flag
        public bool completed = false;

        public string filePath = "";
        public Stream fileStream = null;    

        public long lastTimeStamp = 0;      // Last activity timestamp in seconds
        public ulong lastPacket = 0;        // Last processed packet number

        public int channel = 0;

        public FileTransfer()
        {
            uid = Guid.NewGuid().ToString("N");
            fileName = "New File";
            fileSize = 0;
            preview = null;
            completed = false;
            lastTimeStamp = 0;
            lastPacket = 0;
        }

        public FileTransfer(string in_name, Stream in_stream)
        {
            uid = Guid.NewGuid().ToString("N");
            fileName = in_name;
            fileStream = in_stream;
            fileSize = (ulong)fileStream.Length;
            preview = null;
            completed = false;
            lastTimeStamp = 0;
            lastPacket = 0;
        }

        public FileTransfer(string in_uid, string in_name, ulong in_size, byte[] in_preview = null)
        {
            uid = in_uid;
            fileName = in_name;
            fileSize = in_size;
            preview = in_preview;
            completed = false;
            lastTimeStamp = 0;
            lastPacket = 0;
        }

        public FileTransfer(byte[] bytes)
        {
            try
            {
                using (MemoryStream m = new MemoryStream(bytes))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        uid = reader.ReadString();
                        fileName = reader.ReadString();
                        fileSize = reader.ReadUInt64();

                        int data_length = reader.ReadInt32();
                        if (data_length > 0)
                            preview = reader.ReadBytes(data_length);

                        packetSize = reader.ReadInt32();

                        channel = reader.ReadInt32();
                    }
                }
            }
            catch (Exception e)
            {
                Logging.error("Exception occured while trying to construct FileTransfer from bytes: " + e);
            }
        }

        public byte[] getBytes()
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(uid);

                    writer.Write(fileName);

                    writer.Write(fileSize);

                    // Write the data
                    if (preview != null)
                    {
                        writer.Write(preview.Length);
                        writer.Write(preview);
                    }
                    else
                    {
                        writer.Write(0);
                    }

                    writer.Write(packetSize);

                    writer.Write(channel);
                }
                return m.ToArray();
            }
        }

        public byte[] getPacketData(ulong packet_num)
        {

            if ((ulong)Config.packetDataSize * packet_num >= fileSize)
                return null;

            ulong bytes_to_send_length = fileSize - ((ulong)Config.packetDataSize * packet_num);

            int packet_size = Config.packetDataSize;
            if(bytes_to_send_length < (ulong)packet_size)
            {
                packet_size = (int)bytes_to_send_length;
            }

            byte[] data = new byte[packet_size];

            fileStream.Seek((int)packet_num * Config.packetDataSize, SeekOrigin.Begin);
            fileStream.Read(data, 0, packet_size);

            return data;
        }

        public void updateActivity(ulong last_packet)
        {
            lastTimeStamp = Clock.getTimestamp();
            lastPacket = last_packet;
        }

    }

    class FilePacket
    {
        public string transfer_uid;
        public ulong packet_number = 0;
        public bool added = false;
        public long timestamp = 0;

        public FilePacket(string uid, ulong number)
        {
            transfer_uid = uid;
            packet_number = number;
            added = false;
            timestamp = Clock.getTimestamp();
        }

    }

    class TransferManager
    {
        private static Thread tm_thread = null;
        private static bool running = false;

        static List<FileTransfer> outgoingTransfers = new List<FileTransfer>();
        static List<FileTransfer> incomingTransfers = new List<FileTransfer>();

        static List<FilePacket> incomingPacketsLog = new List<FilePacket>();

        public static string downloadsPath = "Downloads";

        public static void start()
        {
            if (running)
            {
                return;
            }

            downloadsPath = Path.Combine(Config.spixiUserFolder, "Downloads");

            running = true;
            // Start the thread
            tm_thread = new Thread(onUpdate);
            tm_thread.Name = "TransferManager_Update_Thread";
            tm_thread.Start();
        }

        public static void onUpdate()
        {
            while (running)
            {
                lock (incomingTransfers)
                {
                    foreach(FileTransfer transfer in incomingTransfers)
                    {
                        if (transfer.completed || transfer.fileStream == null)
                            continue;

                        if (transfer.lastTimeStamp == 0)
                            continue;

                        if(Clock.getTimestamp() - transfer.lastTimeStamp > Config.packetRequestTimeout)
                        {
                            requestFileData(transfer.sender, transfer.uid, transfer.lastPacket);
                        }
                    }

                }
                Thread.Sleep(1000);
            }

            // Cleanup
            lock (outgoingTransfers)
            {
                foreach (var transfer in outgoingTransfers)
                {
                    if (transfer.fileStream != null)
                    {
                        transfer.fileStream.Dispose();
                    }
                }
                outgoingTransfers.Clear();
            }

            resetIncomingTransfers();
        }

        public static void stop()
        {
            if (!running)
            {
                return;
            }

            running = false;
        }

        public static void resetIncomingTransfers()
        {
            lock (incomingTransfers)
            {
                foreach (var transfer in incomingTransfers)
                {
                    if (transfer.fileStream != null)
                    {
                        transfer.fileStream.Dispose();
                    }
                }
                incomingTransfers.Clear();

                incomingPacketsLog.Clear();
            }
        }

        public static FileTransfer prepareFileTransfer(string filename, Stream stream, string filepath = null, string transfer_id = "")
        {
            FileTransfer transfer = new FileTransfer(filename, stream);
            if (filepath != null)
                transfer.filePath = filepath;

            if (transfer_id != null && transfer_id != "")
                transfer.uid = transfer_id;
            lock (outgoingTransfers)
            {
                if (outgoingTransfers.Find(x => x.uid.SequenceEqual(transfer.uid)) != null)
                {
                    Logging.warn("Outgoing file transfer {0} already prepared.", transfer.uid);
                    return null;
                }

                outgoingTransfers.Add(transfer);
            }
            return transfer;
        }

        public static FileTransfer prepareIncomingFileTransfer(byte[] data, Address sender)
        {
            FileTransfer transfer = new FileTransfer(data);
            transfer.incoming = true;
            transfer.sender = sender;

            lock (incomingTransfers)
            {
                if (incomingTransfers.Find(x => x.uid.SequenceEqual(transfer.uid)) != null)
                {
                    Logging.warn("Incoming file transfer {0} already prepared.", transfer.uid);
                    return null;
                }
                incomingTransfers.Add(transfer);
            }

            Logging.info("File Transfer Size: {0} {1}", transfer.fileName, transfer.fileSize);

            return transfer;
        }

        // Check if a packet already exists in the incomingPackets log
        public static bool checkPacketLog(FilePacket packet)
        {
            foreach (FilePacket ipacket in incomingPacketsLog)
            {
                if (ipacket.transfer_uid.Equals(packet.transfer_uid) && ipacket.packet_number == packet.packet_number && ipacket.added)
                {
                    return true;
                }
            }
            return false;
        }

        public static void removePacketsForFileTransfer(string uid)
        {
            incomingPacketsLog.RemoveAll(item => item.transfer_uid == uid);
        }

        public static bool sendFileData(Friend friend, string uid, ulong packet_number)
        {
            FileTransfer transfer = TransferManager.getOutgoingTransfer(uid);
            if (transfer == null)
                return false;



            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(uid);

                    writer.Write(packet_number);

                    Logging.info("Sending file packet #{0}", packet_number);
                    byte[] data = transfer.getPacketData(packet_number);
                    
                    // Write the data
                    if (data != null)
                    {
                        writer.Write(data.Length);
                        writer.Write(data);
                    }
                    else
                    {
                        writer.Write(0);
                    }

                }

                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.fileData, m.ToArray());


                StreamMessage message = new StreamMessage(friend.protocolVersion);
                message.type = StreamMessageCode.data;
                message.recipient = friend.walletAddress;
                message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                message.data = spixi_message.getBytes();
                message.requireRcvConfirmation = true;

                CoreStreamProcessor.sendMessage(friend, message, false, false, false, !message.requireRcvConfirmation);

            }

            var chat_page = Utils.getChatPage(friend);
            if (chat_page != null)
            {
                ulong totalPackets = transfer.fileSize / (ulong)transfer.packetSize;
                ulong fp = 0;
                bool complete = false;
                if(totalPackets == packet_number)
                {
                    fp = 100;
                    complete = true;
                }else
                {
                    fp = packet_number * 100 / totalPackets;
                }

                chat_page.updateFile(uid, fp.ToString(), complete);
            }

            return true;
        }

        public static bool receiveFileData(byte[] data, Address sender)
        {
            Logging.info("Received File Data");

            byte[] file_data = null;
            try
            {
                using (MemoryStream m = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(m))
                    {
                        string uid = reader.ReadString();
                        ulong packet_number = reader.ReadUInt64();

                        FilePacket packet = new FilePacket(uid, packet_number);
                        if (checkPacketLog(packet))
                            return false;

                        int data_length = reader.ReadInt32();
                        if (data_length > 0)
                            file_data = reader.ReadBytes(data_length);

                        Logging.info("File Uid: {0} Packet #{1}", uid, packet_number);

                        FileTransfer transfer = TransferManager.getIncomingTransfer(uid);
                        if (transfer == null)
                            return false;

                        lock(transfer)
                        {    

                            // Check if the transfer is already completed
                            if (transfer.completed)
                                return false;

                            // Check if this is the next packet to process
                            if (transfer.lastPacket != packet_number)
                                return false;

                            if ((uint)transfer.packetSize * packet_number >= transfer.fileSize)
                            {
                                return false;
                            }


                            long bytes_to_write_length = (long)(transfer.fileSize - ((uint)transfer.packetSize * packet_number));

                            int packet_size = transfer.packetSize;
                            if (bytes_to_write_length < packet_size)
                            {
                                packet_size = (int)bytes_to_write_length;
                            }

                            incomingPacketsLog.Add(packet);

                            transfer.fileStream.Seek(transfer.packetSize * (int)packet_number, SeekOrigin.Begin);
                            transfer.fileStream.Write(file_data, 0, packet_size);

                            transfer.updateActivity(packet_number + 1);

                            ulong new_packet_number = packet_number + 1;
                            if (new_packet_number * (ulong)transfer.packetSize >= transfer.fileSize)
                            {
                                completeFileTransfer(sender, uid);
                                sendFileTransferCompleted(sender, uid);
                                return true;
                            }
                            requestFileData(sender, uid, new_packet_number);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logging.error("Exception occured while receiving file data from bytes: " + e);
            }

            return true;
        }

        public static bool receiveAcceptFile(Friend friend, string uid)
        {
            FileTransfer transfer = TransferManager.getOutgoingTransfer(uid);
            if (transfer == null)
                return false;
          
            // Send first packet
            return TransferManager.sendFileData(friend, uid, 0);
        }


        public static FileTransfer getOutgoingTransfer(string uid)
        {
            FileTransfer transfer = outgoingTransfers.Where(x => x.uid.Contains(uid)).FirstOrDefault();
            return transfer;
        }

        public static FileTransfer getIncomingTransfer(string uid)
        {
            FileTransfer transfer = incomingTransfers.Where(x => x.uid.Contains(uid)).FirstOrDefault();
            return transfer;
        }

        public static void completeFileTransfer(Address sender, string uid)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend == null)
                return;

            bool incoming = true;
            FileTransfer transfer = TransferManager.getIncomingTransfer(uid);
            if (transfer == null)
            {
                incoming = false;
                transfer = TransferManager.getOutgoingTransfer(uid);
                if (transfer == null)
                {
                    return;
                }
            }

            transfer.fileStream.Dispose();
            
            if(incoming && transfer.fileName != null && transfer.fileName != "")
            {
                string final_file_path = Path.Combine(downloadsPath, transfer.fileName);
                int instance_num = 0;
                while (File.Exists(final_file_path))
                {
                    instance_num++;
                    final_file_path = Path.Combine(downloadsPath, Path.GetFileNameWithoutExtension(transfer.fileName) + "-" + instance_num.ToString() + Path.GetExtension(transfer.fileName));
                }
                File.Move(transfer.filePath, final_file_path);
                transfer.filePath = final_file_path;
            }

            transfer.completed = true;

            removePacketsForFileTransfer(uid);

            FriendMessage fm = friend.getMessages(transfer.channel).Find(x => x.transferId == uid);
            fm.completed = true;
            fm.filePath = transfer.filePath;

            IxianHandler.localStorage.requestWriteMessages(friend.walletAddress, transfer.channel);

            if(incoming)
            {
                lock (incomingTransfers)
                {
                    incomingTransfers.Remove(transfer);
                }
            }else
            {
                lock (outgoingTransfers)
                {
                    outgoingTransfers.Remove(transfer);
                }
            }

            var chat_page = Utils.getChatPage(friend);
            if (chat_page != null)
            {
                chat_page.updateFile(uid, "100", true);
            }
        }

        public static void sendFileTransferCompleted(Address sender, string uid)
        {
            Friend friend = FriendList.getFriend(sender);
            if (friend == null)
                return;

            SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.fileFullyReceived, Crypto.stringToHash(uid));

            StreamMessage message = new StreamMessage(friend.protocolVersion);
            message.type = StreamMessageCode.data;
            message.recipient = friend.walletAddress;
            message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            message.data = spixi_message.getBytes();

            CoreStreamProcessor.sendMessage(friend, message, true, true, false);
        }

        public static void requestFileData(Address sender, string uid, ulong packet_number)
        {
            Logging.info("Requesting File Data, packet #{0}", packet_number);
            Friend friend = FriendList.getFriend(sender);
            if (friend == null)
                return;

            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(uid);

                    writer.Write(packet_number);

                }

                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.requestFileData, m.ToArray());

                StreamMessage message = new StreamMessage(friend.protocolVersion);
                message.type = StreamMessageCode.data;
                message.recipient = friend.walletAddress;
                message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                message.data = spixi_message.getBytes();
                message.requireRcvConfirmation = true;

                CoreStreamProcessor.sendMessage(friend, message, false, false, false, !message.requireRcvConfirmation);

                var chat_page = Utils.getChatPage(friend);
                if (chat_page != null)
                {
                    FileTransfer transfer = TransferManager.getIncomingTransfer(uid);
                    if (transfer == null)
                        return;

                    ulong totalPackets = transfer.fileSize / (ulong)transfer.packetSize;
                    ulong fp = 0;
                    if (packet_number > 0
                        && totalPackets > 0)
                    {
                        fp = (packet_number - 1) * 100 / totalPackets;
                    }
                    chat_page.updateFile(uid, fp.ToString(), false);
                }
            }
        }

        public static void acceptFile(Friend friend, string uid)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(uid);
                }

                FileTransfer transfer = getIncomingTransfer(uid);
                if (transfer == null)
                    return;

                Logging.info("Accepting file {0}", transfer.fileName);

                transfer.lastTimeStamp = Clock.getTimestamp();

                transfer.filePath = Path.Combine(downloadsPath, transfer.fileName + "." + uid + ".ixipart");

                transfer.fileStream = File.Create(transfer.filePath);
                transfer.fileStream.SetLength((long)transfer.fileSize);

                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.acceptFile, m.ToArray());

                StreamMessage message = new StreamMessage(friend.protocolVersion);
                message.type = StreamMessageCode.data;
                message.recipient = friend.walletAddress;
                message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                message.data = spixi_message.getBytes();

                CoreStreamProcessor.sendMessage(friend, message);
            }
        }

    }
}
