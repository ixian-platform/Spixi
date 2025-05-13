using IXICore;
using IXICore.Meta;
using IXICore.Storage;
using Spixi;

namespace SPIXI.Meta
{
    internal class SpixiTransactionInclusionCallbacks : TransactionInclusionCallbacks
    {
        public void receivedTransactionInclusionVerificationResponse(byte[] txid, bool verified)
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
            Page p = App.Current.MainPage.Navigation.NavigationStack.Last();
            if (p.GetType() == typeof(SingleChatPage))
            {
                ((SingleChatPage)p).updateTransactionStatus(Transaction.getTxIdString(txid), verified);
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
