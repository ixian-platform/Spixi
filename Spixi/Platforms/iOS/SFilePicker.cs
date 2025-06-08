using Foundation;
using IXICore.Meta;
using SPIXI.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UIKit;

namespace Spixi
{
    public class SFilePicker
    {
        static TaskCompletionSource<SpixiImageData> taskCompletionSource;
        static UIImagePickerController imagePicker;

        public static Task<SpixiImageData> PickImageAsync()
        {
            // Create and define UIImagePickerController
            imagePicker = new UIImagePickerController
            {
                SourceType = UIImagePickerControllerSourceType.PhotoLibrary,
                MediaTypes = UIImagePickerController.AvailableMediaTypes(UIImagePickerControllerSourceType.PhotoLibrary)
            };

            // Set event handlers
            imagePicker.FinishedPickingMedia += OnImagePickerFinishedPickingMedia;
            imagePicker.Canceled += OnImagePickerCancelled;

            // Present UIImagePickerController;
            UIWindow window = UIApplication.SharedApplication.KeyWindow;
            var viewController = window.RootViewController;
            viewController.PresentModalViewController(imagePicker, true);

            // Return Task object
            taskCompletionSource = new TaskCompletionSource<SpixiImageData>();
            return taskCompletionSource.Task;
        }

        public static async Task<SpixiImageData> PickFileAsync()
        {
            FileResult fileData = await FilePicker.PickAsync();
            if (fileData == null)
                return null; // User canceled file picking

            SpixiImageData spixi_img_data = new SpixiImageData() { name = Path.GetFileName(fileData.FullPath), path = fileData.FullPath, stream = await fileData.OpenReadAsync() };

            // Return Task object
            return spixi_img_data;
        }

        static void OnImagePickerFinishedPickingMedia(object sender, UIImagePickerMediaPickedEventArgs args)
        {
            UIImage image = args.EditedImage ?? args.OriginalImage;

            if (image != null)
            {
                // Convert UIImage to .NET Stream object
                NSData data = image.AsJPEG(1);

                SpixiImageData spixi_img_data = new SpixiImageData() { name = Path.GetFileName(args.ImageUrl.AbsoluteString), path = "", stream = data.AsStream() };

                // Set the Stream as the completion of the Task
                taskCompletionSource.SetResult(spixi_img_data);
            }
            else
            {
                var videoURL = (NSUrl)args.Info.ObjectForKey(UIImagePickerController.MediaURL);
                NSData videoData = NSData.FromUrl(videoURL);

                SpixiImageData spixi_img_data = new SpixiImageData() { name = Path.GetFileName(videoURL.AbsoluteString), path = "", stream = videoData.AsStream() };

                // Set the Stream as the completion of the Task
                taskCompletionSource.SetResult(spixi_img_data);
            }
            imagePicker.DismissModalViewController(true);
        }

        static void OnImagePickerCancelled(object sender, EventArgs args)
        {
            taskCompletionSource.SetResult(null);
            imagePicker.DismissModalViewController(true);
        }

        public static byte[] ResizeImage(byte[] image_data, int new_width, int new_height, int quality)
        {
            UIImage original_image = ImageFromByteArray(image_data);

            if (original_image == null)
            {
                return null;
            }

            // Calculate crop section

            float orig_width = (float)original_image.Size.Width;
            float orig_height = (float)original_image.Size.Height;

            float width_ratio = new_width / orig_width;
            float height_ratio = new_height / orig_height;

            float ratio = (float)Math.Max(width_ratio, height_ratio);

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

            UIGraphics.BeginImageContext(new System.Drawing.SizeF(cropped_width, cropped_height));
            var context = UIGraphics.GetCurrentContext();
            var clippedRect = new RectangleF(0, 0, cropped_width, cropped_height);
            context.ClipToRect(clippedRect);
            var imgSize = original_image.Size;
            var drawRect = new RectangleF(-crop_x, -crop_y, (float)imgSize.Width, (float)imgSize.Height);
            original_image.Draw(drawRect);
            var cropped_image = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();

            UIGraphics.BeginImageContext(new System.Drawing.SizeF(new_width, new_height));
            cropped_image.Draw(new RectangleF(0, 0, new_width, new_height));
            var resized_image = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();

            var bytes_imagen = resized_image.AsJPEG((NFloat)quality / 100).ToArray();
            resized_image.Dispose();
            cropped_image.Dispose();

            return bytes_imagen;
        }

        public static UIKit.UIImage ImageFromByteArray(byte[] data)
        {
            if (data == null)
            {
                return null;
            }

            UIKit.UIImage image;
            try
            {
                image = new UIKit.UIImage(Foundation.NSData.FromArray(data));
            }
            catch (Exception e)
            {
                Logging.error("Exception occured in ImageFromBytes: " + e);
                return null;
            }

            return image;
        }

    }
}
