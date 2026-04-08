using Microsoft.UI.Xaml.Media.Imaging;
using SPIXI.Interfaces;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.IO;
using System;
using System.Linq;

namespace Spixi
{
    public class SFilePicker
    {
        static TaskCompletionSource<SpixiImageData> taskCompletionSource;

        public static async Task<SpixiImageData> PickImageAsync()
        {
            var options = new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
            };
            FileResult fileData = await FilePicker.PickAsync(options);
            if (fileData == null)
                return null; // User canceled file picking

            SpixiImageData spixi_img_data = new() { name = Path.GetFileName(fileData.FullPath), path = fileData.FullPath, stream = await fileData.OpenReadAsync() };

            // Return Task object
            return spixi_img_data;
        }

        public static async Task<SpixiImageData> PickFileAsync()
        {
            FileResult fileData = await FilePicker.PickAsync();
            if (fileData == null)
                return null; // User canceled file picking

            SpixiImageData spixi_img_data = new() { name = Path.GetFileName(fileData.FullPath), path = fileData.FullPath, stream = await fileData.OpenReadAsync() };

            // Return Task object
            return spixi_img_data;
        }

        public static byte[] ResizeImage(byte[] imageData, int newWidth, int newHeight, long quality)
        {
            using var originalImage = new Bitmap(new MemoryStream(imageData));

            int originalWidth = originalImage.Width;
            int originalHeight = originalImage.Height;

            float widthRatio = (float)newWidth / originalWidth;
            float heightRatio = (float)newHeight / originalHeight;

            float ratio = Math.Max(widthRatio, heightRatio);

            int resizedPreCropWidth = (int)Math.Round(originalWidth * ratio);
            int resizedPreCropHeight = (int)Math.Round(originalHeight * ratio);

            // Full area to crop on resized image
            int resizedCropX = resizedPreCropWidth - newWidth;
            int resizedCropY = resizedPreCropHeight - newHeight;

            int croppedWidth = (int)((resizedPreCropWidth - resizedCropX) / ratio);
            int croppedHeight = (int)((resizedPreCropHeight - resizedCropY) / ratio);

            // Half of area to crop on original image
            int cropX = (int)(resizedCropX / ratio / 2);
            int cropY = (int)(resizedCropY / ratio / 2);

            // Crop and resize
            var resizedImage = new Bitmap(newWidth, newHeight);
            using var graphics = Graphics.FromImage(resizedImage);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(originalImage, new Rectangle(0, 0, newWidth, newHeight), new Rectangle(cropX, cropY, croppedWidth, croppedHeight), GraphicsUnit.Pixel);

            // Convert to JPEG
            var encoder = ImageCodecInfo.GetImageDecoders().First(codec => codec.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            using var outputStream = new MemoryStream();
            resizedImage.Save(outputStream, encoder, encoderParameters);
            return outputStream.ToArray();
        }


    }
}
