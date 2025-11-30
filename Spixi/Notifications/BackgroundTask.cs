using System.Threading;
using System.Threading.Tasks;

namespace SPIXI.Notifications
{
    public class BackgroundTask
    {
        /*public async Task Run(CancellationToken token)
        {
            await Task.Run(async () => {

                for (long i = 0; i < long.MaxValue; i++)
                {
                    token.ThrowIfCancellationRequested();

                    await Task.Delay(1000);
                    var message = new TickedMessage
                    {
                        Message = i.ToString()
                    };

                    MainThread.BeginInvokeOnMainThread(() => {
                        MessagingCenter.Send<TickedMessage>(message, "TickedMessage");
                    });
                }
            }, token);
        }*/
    }
}
