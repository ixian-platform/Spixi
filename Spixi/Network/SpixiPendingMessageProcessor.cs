
using IXICore;
using IXICore.Streaming;

namespace SPIXI.Network
{
    internal class SpixiPendingMessageProcessor : PendingMessageProcessor
    {
        public SpixiPendingMessageProcessor(string root_storage_path, bool enable_push_notification_server) : base(root_storage_path, enable_push_notification_server)
        {
        }

        protected override void onMessageSent(Friend friend, int channel, StreamMessage msg)
        {
            UIHelpers.shouldRefreshContacts = true;
            UIHelpers.updateMessage(friend, channel, friend.getMessage(channel, msg.id));
        }
    }
}
