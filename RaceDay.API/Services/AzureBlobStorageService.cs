
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.API.Services
{
    public class AzureBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _profilePicturesContainer;
        private readonly string _eventImagesContainer;

        public AzureBlobStorageService(IConfiguration configuration)
        {
            var connectionString =
                configuration["AzureStorage:ConnectionString"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Azure Storage connection string is missing.");
            }

            _blobServiceClient =
                new BlobServiceClient(connectionString);

            _profilePicturesContainer =
                configuration["AzureStorage:ProfilePicturesContainer"]
                ?? "profile-pictures";

            _eventImagesContainer =
                configuration["AzureStorage:EventImagesContainer"]
                ?? "event-images";
        }

        // ==========================================
        // UPLOAD PROFILE PICTURE
        // ==========================================

        public async Task<string> UploadProfilePictureAsync(
            IFormFile file,
            int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException(
                    "Invalid user ID.", nameof(userId));
            }

            return await UploadImageAsync(
                file,
                _profilePicturesContainer,
                $"users/{userId}");
        }

        // ==========================================
        // UPLOAD EVENT IMAGE
        // ==========================================

        public async Task<string> UploadEventImageAsync(
            IFormFile file,
            int eventId)
        {
            if (eventId <= 0)
            {
                throw new ArgumentException(
                    "Invalid event ID.", nameof(eventId));
            }

            return await UploadImageAsync(
                file,
                _eventImagesContainer,
                $"events/{eventId}");
        }

        // ==========================================
        // SHARED IMAGE UPLOAD METHOD
        // ==========================================

        private async Task<string> UploadImageAsync(
            IFormFile file,
            string containerName,
            string folder)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException(
                    "Please select an image.");
            }

            const long maxFileSize = 5 * 1024 * 1024;

            if (file.Length > maxFileSize)
            {
                throw new ArgumentException(
                    "Image size must not exceed 5 MB.");
            }

            var extension = Path.GetExtension(
                file.FileName).ToLowerInvariant();

            var allowedTypes = new Dictionary<string, string>
            {
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png", "image/png" },
                { ".webp", "image/webp" }
            };

            if (!allowedTypes.TryGetValue(
                extension, out var contentType))
            {
                throw new ArgumentException(
                    "Only JPG, JPEG, PNG and WebP images are allowed.");
            }

            // Confirm the actual file signature.
            // Do not rely only on the filename or
            // browser-supplied content type.
            await using var stream = file.OpenReadStream();

            var header = new byte[12];

            var bytesRead = await stream.ReadAsync(
                header.AsMemory(0, header.Length));

            if (!HasValidImageSignature(
                header, bytesRead, extension))
            {
                throw new ArgumentException(
                    "The selected file is not a valid supported image.");
            }

            stream.Position = 0;

            var containerClient =
                _blobServiceClient.GetBlobContainerClient(
                    containerName);

            // Containers were already created privately
            // in the Azure Portal.
            var blobName =
                $"{folder}/{Guid.NewGuid():N}{extension}";

            var blobClient =
                containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType
                    }
                });

            // Return the blob name rather than a public URL.
            // The container remains private.
            return blobName;
        }

        // ==========================================
        // VALIDATE IMAGE FILE SIGNATURE
        // ==========================================

        private static bool HasValidImageSignature(
            byte[] header,
            int bytesRead,
            string extension)
        {
            if (extension == ".jpg" ||
                extension == ".jpeg")
            {
                return bytesRead >= 3 &&
                       header[0] == 0xFF &&
                       header[1] == 0xD8 &&
                       header[2] == 0xFF;
            }

            if (extension == ".png")
            {
                byte[] png =
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                };

                return bytesRead >= 8 &&
                       header.Take(8).SequenceEqual(png);
            }

            if (extension == ".webp")
            {
                return bytesRead >= 12 &&
                       header[0] == (byte)'R' &&
                       header[1] == (byte)'I' &&
                       header[2] == (byte)'F' &&
                       header[3] == (byte)'F' &&
                       header[8] == (byte)'W' &&
                       header[9] == (byte)'E' &&
                       header[10] == (byte)'B' &&
                       header[11] == (byte)'P';
            }

            return false;
        }

        // ==========================================
        // DOWNLOAD PRIVATE IMAGE
        // ==========================================

        public async Task<(Stream Content, string ContentType)>
            DownloadImageAsync(
                string containerName,
                string blobName)
        {
            var containerClient =
                _blobServiceClient.GetBlobContainerClient(
                    containerName);

            var blobClient =
                containerClient.GetBlobClient(blobName);

            var response =
                await blobClient.DownloadStreamingAsync();

            var contentType =
                response.Value.Details.ContentType
                ?? "application/octet-stream";

            return (
                response.Value.Content,
                contentType
            );
        }

        // ==========================================
        // DELETE IMAGE
        // ==========================================

        public async Task DeleteImageAsync(
            string containerName,
            string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
                return;

            var containerClient =
                _blobServiceClient.GetBlobContainerClient(
                    containerName);

            var blobClient =
                containerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots);
        }
    }
}
