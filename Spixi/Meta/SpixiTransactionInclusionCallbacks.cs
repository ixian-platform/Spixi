using IXICore;
using IXICore.Meta;
using IXICore.Storage;
using IXICore.Streaming;

namespace SPIXI.Meta
{
    internal class SpixiTransactionInclusionCallbacks : TransactionInclusionCallbacks
    {
        public void receivedTIVResponse(byte[] txid, bool verified)
        {
            // TODO implement error
            // TODO implement blocknum
            Transaction tx = TransactionCache.getUnconfirmedTransaction(txid);
            if (tx == null)
            {
                return;
            }

            if (!verified)
            {
                tx.applied = 0;
            }

            TransactionCache.addTransaction(tx);
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

            IxianHandler.balances.First().lastUpdate = 0;

            if (friend != null)
            {
                SingleChatPage chatPage = Utils.getChatPage(friend);
                if (chatPage != null)
                {
                    chatPage.updateTransactionStatus(Transaction.getTxIdString(txid), verified);
                }

                IxiNumber amount = tx.toList.First().Value.amount;
                MiniAppPage page = Node.MiniAppManager.getAppPage(friend.walletAddress);
                if (page == null)
                {
                    Logging.info("App session does not exist.");
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

        public void receivedBlockHeader(Block block_header, bool verified)
        {
            foreach (Balance balance in IxianHandler.balances)
            {
                if (balance.blockChecksum != null && balance.blockChecksum.SequenceEqual(block_header.blockChecksum))
                {
                    balance.verified = true;
                }
            }

            if (block_header.blockNum >= IxianHandler.getHighestKnownNetworkBlockHeight())
            {
                IxianHandler.status = NodeStatus.ready;
            }
            Node.processPendingTransactions();
        }
    }
}
