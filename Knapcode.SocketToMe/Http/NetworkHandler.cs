using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.SocketToMe.Support;

namespace Knapcode.SocketToMe.Http
{
    public class NetworkHandler : HttpMessageHandler
    {
        private const int BufferSize = 4096;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri.Scheme == "https")
            {
                throw new NotSupportedException("HTTPS is not supported.");
            }
            
            var tcpClient = new TcpClient(request.RequestUri.DnsSafeHost, request.RequestUri.Port);
            NetworkStream networkStream = tcpClient.GetStream();

            // send the request
            await WriteRequestAsync(request, networkStream);

            // read the request
            return await ReadResponseAsync(request, networkStream);
        }

        private async Task WriteRequestAsync(HttpRequestMessage request, Stream stream)
        {
            byte[] bytes = null;
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true), BufferSize, true))
            {
                await writer.WriteLineAsync(string.Format("{0} {1} HTTP/{2}", request.Method.Method, request.RequestUri.PathAndQuery, request.Version));

                await writer.WriteLineAsync(string.Format("Host: {0}", request.RequestUri.Host));
                foreach (var header in request.Headers)
                {
                    await writer.WriteLineAsync(GetHeader(header));
                }

                if (request.Content != null)
                {
                    bytes = await request.Content.ReadAsByteArrayAsync();
                    request.Content.Headers.ContentLength = bytes.Length;

                    foreach (var header in request.Content.Headers)
                    {
                        await writer.WriteLineAsync(GetHeader(header));
                    }
                }

                await writer.WriteLineAsync();
                await writer.FlushAsync();
            }

            if (bytes != null)
            {
                await request.Content.CopyToAsync(stream);
            }
        }

        private string GetHeader(KeyValuePair<string, IEnumerable<string>> header)
        {
            return string.Format("{0}: {1}", header.Key, string.Join(",", header.Value));
        }

        private async Task<HttpResponseMessage> ReadResponseAsync(HttpRequestMessage request, Stream stream)
        {
            // initialize the response
            var response = new HttpResponseMessage();

            // read the first line of the response
            var reader = new ByteStreamReader(stream, BufferSize, false);
            string line = await reader.ReadLineAsync();
            string[] pieces = line.Split(new[] { ' ' }, 3);
            if (pieces[0] != "HTTP/1.1")
            {
                throw new HttpRequestException("The HTTP version the response is not supported.");
            }

            response.StatusCode = (HttpStatusCode)int.Parse(pieces[1]);
            response.ReasonPhrase = pieces[2];

            // read the headers
            response.Content = new ByteArrayContent(new byte[0]);
            while ((line = await reader.ReadLineAsync()) != null && line != string.Empty)
            {
                pieces = line.Split(new[] { ":" }, 2, StringSplitOptions.None);
                if (pieces[1].StartsWith(" "))
                {
                    pieces[1] = pieces[1].Substring(1);
                }

                var headers = HttpHeaderCategories.IsContentHeader(pieces[0]) ? (HttpHeaders) response.Content.Headers : response.Headers;
                headers.Add(pieces[0], pieces[1]);
            }

            // read the content
            if (request.Method != HttpMethod.Head && response.Content.Headers.ContentLength.HasValue)
            {
                var remainingStream = reader.GetRemainingStream();
                var limitedStream = new LimitedStream(remainingStream, response.Content.Headers.ContentLength.Value);
                var streamContent = new StreamContent(limitedStream);
                foreach (var header in response.Content.Headers)
                {
                    streamContent.Headers.Add(header.Key, header.Value);
                }

                response.Content = streamContent;
            }

            return response;
        }
    }
}