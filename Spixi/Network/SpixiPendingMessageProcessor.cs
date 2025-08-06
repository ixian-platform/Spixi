
using IXICore;
using IXICore.Meta;
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
            // TODO trigger sent from pending message, not just offline?
            friend.setMessageSent(channel, msg.id);
            UIHelpers.shouldRefreshContacts = true;
            var fm = friend.getMessage(channel, msg.id);
            if (fm != null)
            {
                UIHelpers.updateMessage(friend, channel, fm);
            }
        }

        protected override void onMessageExpired(Friend friend, int channel, StreamMessage msg)
        {
            removeMessage(friend, msg.id);
            friend.setMessageError(channel, msg.id);
            UIHelpers.shouldRefreshContacts = true;
            var fm = friend.getMessage(channel, msg.id);
            if (fm != null)
            {
                UIHelpers.updateMessage(friend, channel, fm);
            }
        }
    }
}
