using System.IO;
using System.Threading.Tasks;
using Android.Content;
using SPIXI.Interfaces;
using Android.Graphics;
using System;
using Microsoft.Maui.Storage;

namespace Spixi
{
    public class SFilePicker
    {
        public static Task<SpixiImageData> PickImageAsync()
        {
            // Define the Intent for getting images
            Intent intent = new Intent();
            intent.SetType("image/*");
            intent.SetAction(Intent.ActionGetContent);

            // Get the MainActivity instance
            MainActivity activity = MainActivity.Instance;

            // Start the picture-picker activity (resumes in MainActivity.cs)
            activity.StartActivityForResult(
                Intent.CreateChooser(intent, "Select Picture"),
                MainActivity.PickImageId);

            // Save the TaskCompletionSource object as a MainActivity property
            activity.PickImageTaskCompletionSource = new TaskCompletionSource<SpixiImageData>();

            // Return Task object
            return activity.PickImageTaskCompletionSource.Task;
        }

        public static async Task<SpixiImageData> PickFileAsync()
        {
            FileResult fileData = await FilePicker.PickAsync();
            if (fileData == null)
                return null; // User canceled file picking

            SpixiImageData spixi_img_data = new SpixiImageData() { name = fileData.FileName, path = fileData.FullPath, stream = await fileData.OpenReadAsync() };

            // Return Task object
            return spixi_img_data;
        }

        public static byte[] ResizeImage(byte[] image_data, int new_width, int new_height, int quality)
        {
            Bitmap original_image = BitmapFactory.DecodeByteArray(image_data, 0, image_data.Length);

            if (original_image == null)
            {
                return null;
            }

            // Calculate crop section

            int orig_width = original_image.Width;
            int orig_height = original_image.Height;

            float width_ratio = (float)new_width / orig_width;
            float height_ratio = (float)new_height / orig_height;

            float ratio = Math.Max(width_ratio, height_ratio);

            int resized_pre_crop_width = (int)Math.Round(orig_width * ratio);
            int resized_pre_crop_height = (int)Math.Round(orig_height * ratio);

            // full area to crop on resized image
            int resized_crop_x = resized_pre_crop_width - new_width;
            int resized_crop_y = resized_pre_crop_height - new_height;

            int cropped_width = (int)((resized_pre_crop_width - resized_crop_x) / ratio);
            int cropped_height = (int)((resized_pre_crop_height - resized_crop_y) / ratio);

            // half of area to crop on original image
            int crop_x = (int)(resized_crop_x / ratio / 2);
            int crop_y = (int)(resized_crop_y / ratio / 2);

            // End of calculate crop section

            Bitmap cropped_image = Bitmap.CreateBitmap(original_image, crop_x, crop_y, cropped_width, cropped_height);
            Bitmap resized_image = Bitmap.CreateScaledBitmap(cropped_image, new_width, new_height, false);

            using (MemoryStream ms = new MemoryStream())
            {
                resized_image.Compress(Bitmap.CompressFormat.Jpeg, quality, ms);
                resized_image.Dispose();
                cropped_image.Dispose();
                return ms.ToArray();
            }
        }

    }
}
