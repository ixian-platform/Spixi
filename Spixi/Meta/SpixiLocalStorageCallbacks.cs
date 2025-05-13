using IXICore;
using IXICore.Meta;
using IXICore.Storage;
using IXICore.Streaming;

namespace SPIXI.Meta
{
    internal class SpixiLocalStorageCallbacks : LocalStorageCallbacks
    {
        public bool receivedNewTransaction(Transaction transaction)
        {
            return Node.tiv.receivedNewTransaction(transaction);
        }

        public void processMessage(FriendMessage friendMessage)
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
                            TransferManager.prepareFileTransfer(t_file_name, fs, friendMessage.filePath, friendMessage.transferId);
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
