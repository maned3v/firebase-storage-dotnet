﻿namespace Firebase.Storage
{
    using Firebase.Storage.Bucket;
    using Newtonsoft.Json;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class FirebaseStorageReference
    {
        internal const string FirebaseStorageEndpoint = "https://firebasestorage.googleapis.com/v0/b/";

        private readonly FirebaseStorage storage;
        private readonly List<string> children;

        internal FirebaseStorageReference(FirebaseStorage storage, string childRoot)
        {
            this.children = new List<string>();

            this.storage = storage;
            this.children.Add(childRoot);
        }

        /// <summary>
        /// Starts uploading given stream to target location.
        /// </summary>
        /// <param name="stream"> Stream to upload. </param>
        /// <param name="cancellationToken"> Cancellation token which can be used to cancel the operation. </param>
        /// <param name="mimeType"> Optional type of data being uploaded, will be used to set HTTP Content-Type header. </param>
        /// <returns> <see cref="FirebaseStorageTask"/> which can be used to track the progress of the upload. </returns>
        public FirebaseStorageTask PutAsync(Stream stream, CancellationToken cancellationToken, string mimeType = null)
        {
            return new FirebaseStorageTask(this.storage.Options, this.GetTargetUrl(), this.GetFullDownloadUrl(), stream, cancellationToken, mimeType);
        }

        /// <summary>
        /// Starts uploading given stream to target location.
        /// </summary>
        /// <param name="stream"> Stream to upload. </param>
        /// <returns> <see cref="FirebaseStorageTask"/> which can be used to track the progress of the upload. </returns>
        public FirebaseStorageTask PutAsync(Stream fileStream)
        {
            return this.PutAsync(fileStream, CancellationToken.None);
        }

        /// <summary>
        /// Gets the meta data for given file.
        /// </summary>
        /// <returns></returns>
        public async Task<FirebaseMetaData> GetMetaDataAsync()
        {
            var data = await PerformFetch<FirebaseMetaData>();

            return data;
        }

        /// <summary>
        /// Gets the url to download given file.
        /// </summary>
        public async Task<string> GetDownloadUrlAsync()
        {
            var data = await PerformFetch<Dictionary<string, object>>();

            object downloadTokens;

            if (!data.TryGetValue("downloadTokens", out downloadTokens))
            {
                throw new ArgumentOutOfRangeException($"Could not extract 'downloadTokens' property from response. Response: {JsonConvert.SerializeObject(data)}");
            }

            return this.GetFullDownloadUrl() + downloadTokens;
        }

        /// <summary>
        /// Deletes a file at target location.
        /// </summary>
        public async Task DeleteAsync()
        {
            var url = this.GetDownloadUrl();
            var resultContent = "N/A";

            try
            {
                using (var http = await this.storage.Options.CreateHttpClientAsync().ConfigureAwait(false))
                {
                    var result = await http.DeleteAsync(url).ConfigureAwait(false);

                    resultContent = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                    result.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                throw new FirebaseStorageException(url, resultContent, ex);
            }
        }

        /// <summary>
        /// List all files descended from this reference.
        /// </summary>
        /// <param name="maxResults">Maximum results per page (absolute maximum is 1000)</param>
        /// <param name="pageToken">Next page token</param>
        /// <returns>A <see cref="StorageBucketList" /> object with the requested results.</returns>
        public async Task<StorageBucketList> ListFiles(int maxResults = 1000, string pageToken = null)
            => await storage.ListFiles(this, maxResults, pageToken);

        /// <summary>
        /// List all prefixes (folders) immediately descended from this reference.
        /// </summary>
        /// <param name="maxResults">Maximum results per page (absolute maximum is 1000)</param>
        /// <param name="pageToken">Next page token</param>
        /// <returns>A <see cref="StorageBucketList" /> object with the requested results.</returns>
        public async Task<StorageBucketList> ListPrefixes(int maxResults = 1000, string pageToken = null)
            => await storage.ListPrefixes(this, maxResults, pageToken);

        /// <summary>
        /// Constructs firebase path to the file.
        /// </summary>
        /// <param name="name"> Name of the entity. This can be folder or a file name or full path.</param>
        /// <example>
        ///     storage
        ///         .Child("some")
        ///         .Child("path")
        ///         .Child("to/file.png");
        /// </example>
        /// <returns> <see cref="FirebaseStorageReference"/> for fluid syntax. </returns>
        public FirebaseStorageReference Child(string name)
        {
            this.children.Add(name);
            return this;
        }

        private async Task<T> PerformFetch<T>()
        {
            var url = this.GetDownloadUrl();
            var resultContent = "N/A";

            try
            {
                using (var http = await this.storage.Options.CreateHttpClientAsync().ConfigureAwait(false))
                {
                    var result = await http.GetAsync(url);
                    resultContent = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var data = JsonConvert.DeserializeObject<T>(resultContent);

                    result.EnsureSuccessStatusCode();

                    return data;
                }
            }
            catch (Exception ex)
            {
                throw new FirebaseStorageException(url, resultContent, ex);
            }
        }

        internal string GetTargetUrl()
        {
            return $"{FirebaseStorageEndpoint}{this.storage.StorageBucket}/o?name={this.GetEscapedPath()}";
        }

        internal string GetDownloadUrl()
        {
            return $"{FirebaseStorageEndpoint}{this.storage.StorageBucket}/o/{this.GetEscapedPath()}";
        }

        internal string GetFullDownloadUrl()
        {
            return this.GetDownloadUrl() + "?alt=media&token=";
        }

        internal string GetEscapedPath()
        {
            return Uri.EscapeDataString(string.Join("/", this.children));
        }
    }
}
