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
                SingleChatPage page = Utils.getChatPage(friend);
                if (page != null)
                {
                    page.updateTransactionStatus(Transaction.getTxIdString(txid), verified);
                }
            }

            IxianHandler.balances.First().lastUpdate = 0;
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
