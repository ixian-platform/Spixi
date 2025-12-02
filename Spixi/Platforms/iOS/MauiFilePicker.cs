// Custom iOS file picker implementation for .NET MAUI 10, due to official version not working on iOS.
// Provides proper security-scoped resource access and bookmark handling for iOS file selection.
// Based on official .NET MAUI code, adapted for iOS-specific requirements including:
// - Security-scoped resource tokens for sandboxed file access
// - NSFileCoordinator for safe file access coordination
// - Bookmark data persistence for accessing files from external providers (iCloud, etc.)
// - Custom FileResult implementation compatible with .NET MAUI 10

using Foundation;
using MobileCoreServices;
using UIKit;

namespace Spixi.Platform.iOS
{
    public static class MauiFilePicker
    {
        public static async Task<IEnumerable<FileResult>> PickAsync(PickOptions options, bool allowMultiple = false)
        {
#pragma warning disable CA1416 // TODO: UTType has [UnsupportedOSPlatform("ios14.0")]
#pragma warning disable CA1422 // Validate platform compatibility
            var allowedUtis = options?.FileTypes?.Value?.ToArray() ?? new string[]
            {
                UTType.Content,
                UTType.Item,
                "public.data"
            };

            var tcs = new TaskCompletionSource<IEnumerable<FileResult>>();

            // Use Open instead of Import so that we can attempt to use the original file.
            // If the file is from an external provider, then it will be downloaded.

            using var documentPicker = new UIDocumentPickerViewController(allowedUtis, UIDocumentPickerMode.Open);
#pragma warning restore CA1422 // Validate platform compatibility
#pragma warning restore CA1416 // Constructor UIDocumentPickerViewController  has [UnsupportedOSPlatform("ios14.0")]
            documentPicker.AllowsMultipleSelection = allowMultiple;

            documentPicker.Delegate = new PickerDelegate
            {
                PickHandler = urls => GetFileResults(urls, tcs)
            };

            var parentController = WindowStateManager.Default.GetCurrentUIViewController();
            parentController.PresentViewController(documentPicker, true, null);

            return await tcs.Task;
        }

        static async void GetFileResults(NSUrl[] urls, TaskCompletionSource<IEnumerable<FileResult>> tcs)
        {
            try
            {
                if (urls == null || urls.Length == 0)
                {
                    tcs.TrySetResult(Array.Empty<FileResult>());
                    return;
                }

                tcs.TrySetResult(await EnsurePhysicalFileResultsAsync(urls));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        public static async Task<FileResult[]> EnsurePhysicalFileResultsAsync(params NSUrl[] urls)
        {
            if (urls == null || urls.Length == 0)
                return Array.Empty<FileResult>();

            var opts = NSFileCoordinatorReadingOptions.WithoutChanges;
            var intents = urls.Select(x => NSFileAccessIntent.CreateReadingIntent(x, opts)).ToArray();

            using var coordinator = new NSFileCoordinator();

            var tcs = new TaskCompletionSource<FileResult[]>();

            coordinator.CoordinateAccess(intents, new NSOperationQueue(), error =>
            {
                if (error != null)
                {
                    tcs.TrySetException(new NSErrorException(error));
                    return;
                }

                var bookmarks = new List<FileResult>();

                foreach (var intent in intents)
                {
                    var url = intent.Url;
                    var result = new BookmarkDataFileResult(url);
                    bookmarks.Add(result);
                }

                tcs.TrySetResult(bookmarks.ToArray());
            });

            return await tcs.Task;
        }

        class PickerDelegate : UIDocumentPickerDelegate
        {
            public Action<NSUrl[]> PickHandler { get; set; }

            public override void WasCancelled(UIDocumentPickerViewController controller)
                => PickHandler?.Invoke(null);

            public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
                => PickHandler?.Invoke(urls);

            public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
                => PickHandler?.Invoke(new NSUrl[] { url });
        }
    }

    class BookmarkDataFileResult : FileResult
    {
        NSData bookmark;
        string filePath;

        internal BookmarkDataFileResult(NSUrl url) : base(GetFilePath(url))
        {
            try
            {
                url.StartAccessingSecurityScopedResource();

                var newBookmark = url.CreateBookmarkData(0, Array.Empty<string>(), null, out var bookmarkError);
                if (bookmarkError != null)
                    throw new NSErrorException(bookmarkError);

                UpdateBookmark(url, newBookmark);
            }
            finally
            {
                url.StopAccessingSecurityScopedResource();
            }
        }

        static string GetFilePath(NSUrl url)
        {
            var doc = new UIDocument(url);
            return doc.FileUrl?.Path ?? url?.Path;
        }

        void UpdateBookmark(NSUrl url, NSData newBookmark)
        {
            bookmark = newBookmark;

            var doc = new UIDocument(url);
            filePath = doc.FileUrl?.Path ?? url?.Path;
        }

        public new Task<Stream> OpenReadAsync()
        {
            var url = NSUrl.FromBookmarkData(bookmark, 0, null, out var isStale, out var error);

            if (error != null)
                throw new NSErrorException(error);

            url.StartAccessingSecurityScopedResource();

            if (isStale)
            {
                var newBookmark = url.CreateBookmarkData(NSUrlBookmarkCreationOptions.SuitableForBookmarkFile, Array.Empty<string>(), null, out error);
                if (error != null)
                    throw new NSErrorException(error);

                UpdateBookmark(url, newBookmark);
            }

            var fileStream = File.OpenRead(filePath);
            Stream stream = new SecurityScopedStream(fileStream, url);
            return Task.FromResult(stream);
        }

        class SecurityScopedStream : Stream
        {
            FileStream stream;
            NSUrl url;

            internal SecurityScopedStream(FileStream stream, NSUrl url)
            {
                this.stream = stream;
                this.url = url;
            }

            public override bool CanRead => stream.CanRead;

            public override bool CanSeek => stream.CanSeek;

            public override bool CanWrite => stream.CanWrite;

            public override long Length => stream.Length;

            public override long Position
            {
                get => stream.Position;
                set => stream.Position = value;
            }

            public override void Flush() =>
                stream.Flush();

            public override int Read(byte[] buffer, int offset, int count) =>
                stream.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) =>
                stream.Seek(offset, origin);

            public override void SetLength(long value) =>
                stream.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) =>
                stream.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (disposing)
                {
                    stream?.Dispose();
                    stream = null;

                    url?.StopAccessingSecurityScopedResource();
                    url = null;
                }
            }
        }
    }
}
