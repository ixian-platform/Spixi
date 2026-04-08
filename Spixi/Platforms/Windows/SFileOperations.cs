using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;

namespace Spixi
{
    public class SFileOperations
    {
        public static void open(string filepath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filepath,
                UseShellExecute = true
            });
        }

        public static async Task<Task<bool>> share(string filepath, string title)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            try
            {
                string fileName = Path.GetFileName(filepath);
                using FileStream fileStream = File.OpenRead(filepath);
                var fileSaverResult = await FileSaver.Default.SaveAsync(fileName, fileStream, cancellationTokenSource.Token);
                if (!fileSaverResult.IsSuccessful)
                {
                    await Toast.Make($"The file was not saved. Error: {fileSaverResult.Exception.Message}").Show(cancellationTokenSource.Token);
                }
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult(false);
            }
            catch (IOException)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

    }
}
