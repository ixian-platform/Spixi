using IXICore;
using IXICore.Activity;
using IXICore.Meta;
using IXICore.Streaming;
using IXICore.Utils;

namespace SPIXI.Meta
{
    internal class SpixiTransactionInclusionCallbacks : TransactionInclusionCallbacks
    {
        public void transactionVerified(Transaction tx)
        {
            var bh = IxianHandler.getBlockHeader(tx.applied);
            Node.activityStorage.updateStatus(tx.id, ActivityStatus.Final, tx.applied, bh.timestamp);

            requestBalanceUpdate(tx);

            refreshTransactionPages(tx, true);
        }

        private void requestBalanceUpdate(Transaction tx)
        {
            if (IxianHandler.isMyAddress(tx.pubKey))
            {
                foreach (var fromEntry in tx.fromList)
                {
                    IxianHandler.balances.TryGet(new Address(tx.pubKey.getInputBytes(), fromEntry.Key))?.lastUpdate = 0;
                }
            }
            else
            {
                foreach (var toEntry in tx.toList)
                {
                    if (IxianHandler.isMyAddress(toEntry.Key))
                    {
                        IxianHandler.balances.TryGet(toEntry.Key)?.lastUpdate = 0;
                    }
                }
            }
        }

        private void refreshTransactionPages(Transaction tx, bool verified)
        {
            UIHelpers.shouldRefreshTransactions = true;
            Friend friend = FriendList.getFriend(tx.pubKey);
            bool myTransaction = IxianHandler.isMyAddress(tx.pubKey);
            if (friend == null)
            {
                foreach (var toEntry in tx.toList)
                {
                    friend = FriendList.getFriend(toEntry.Key);
                    if (friend != null)
                    {
                        break;
                    }
                }
            }

            if (friend != null)
            {
                SingleChatPage chatPage = Utils.getChatPage(friend);
                if (chatPage != null)
                {
                    chatPage.updateTransactionStatus(Transaction.getTxIdString(tx.id), verified);
                }

                IxiNumber amount = tx.toList.First().Value.amount;
                MiniAppPage page = Node.MiniAppManager.getAppPage(friend.walletAddress);
                if (page == null)
                {
                    return;
                }

                if (myTransaction)
                {
                    page.paymentSent(friend.walletAddress, amount, tx.getTxIdString(), tx.getBytes(true, true), verified);
                }
                else
                {
                    page.transactionReceived(friend.walletAddress, amount, tx.getTxIdString(), tx.getBytes(true, true), verified);
                }
            }
        }

        public void transactionRejected(Transaction tx)
        {
            tx.applied = 0;
            Node.activityStorage.updateStatus(tx.id, ActivityStatus.Rejected, 0);
            refreshTransactionPages(tx, false);
        }

        public void transactionExpired(Transaction tx)
        {
            tx.applied = 0;
            Node.activityStorage.updateStatus(tx.id, ActivityStatus.Expired, 0);
            refreshTransactionPages(tx, false);
        }

        public void receivedBlockHeader(Block blockHeader, bool verified)
        {
            foreach (Balance balance in IxianHandler.balances.Values)
            {
                if (balance.blockChecksum != null && balance.blockChecksum.SequenceEqual(blockHeader.blockChecksum))
                {
                    balance.verified = true;
                }
            }

            /*if (blockHeader.blockNum + 10 >= IxianHandler.getHighestKnownNetworkBlockHeight()
                && (IxianHandler.status == NodeStatus.warmUp || IxianHandler.status == NodeStatus.stalled))
            {*/
                IxianHandler.status = NodeStatus.ready;
            //}
        }

        public void blockReorg(Block blockHeader)
        {
            var revertedTransactions = Node.activityStorage.revertTransactionsByBlockHeight(blockHeader.blockNum);
            foreach (var revertedTxId in revertedTransactions)
            {
                var activity = Node.activityStorage.getActivityById(revertedTxId, null, true);
                PendingTransactions.addOutgoingTransaction(activity.transaction, activity.transaction.toList.TakeLast(2).Select(x => x.Key).ToList());
                refreshTransactionPages(activity.transaction, false);
            }
        }
    }
}
