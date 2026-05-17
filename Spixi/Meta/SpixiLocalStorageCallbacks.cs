using IXICore.Meta;
using IXICore.Storage;
using IXICore.Streaming;
using System;
using System.IO;

namespace SPIXI.Meta
{
    internal class SpixiLocalStorageCallbacks : LocalStorageCallbacks
    {
        public void processMessage(Friend friend, int channel, FriendMessage friendMessage)
        {
            if (friendMessage.filePath != "")
            {
                string t_file_name = Path.GetFileName(friendMessage.filePath);
                try
                {
                    if (friendMessage.type == FriendMessageType.fileHeader && friendMessage.completed == false)
                    {
                        if (friendMessage.localSender)
                        {
                            // TODO may not work on Android/iOS due to unauthorized access
                            FileStream fs = new FileStream(friendMessage.filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var ft = TransferManager.prepareFileTransfer(t_file_name, fs, friendMessage.filePath, friendMessage.transferId);
                            if (ft == null)
                            {
                                Logging.error("Failed to prepare file transfer for file '{0}' - friend '{1}', full path '{2}'", t_file_name, friend.walletAddress.ToString(), friendMessage.filePath);
                                return;
                            }
                            if (friend.bot || friend.type == FriendType.Group)
                            {
                                ft.channel = channel;
                                ft.groupAddress = friend.walletAddress;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Error occured while trying to prepare file transfer for file '{0}' - friend '{1}', message contents '{2}' full path '{3}': {4}", t_file_name, friendMessage.filePath, friendMessage.message, friendMessage.filePath, e);
                }
            }
        }
    }
}
