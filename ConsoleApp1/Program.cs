using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", true);

            await MakeCall("/response-body-flushasync");
            await MakeCall("/response-startasync");
            await MakeCall("/response-bodywriter-flushasync");

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        private static async Task MakeCall(string path)
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true;
            handler.ClientCertificates.Add(new X509Certificate2("client.crt"));

            var requestStreamTcs = new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestStreamCompleteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var httpRequest = new HttpRequestMessage();
            httpRequest.Version = new Version(2, 0);
            httpRequest.RequestUri = new Uri("https://localhost:50051" + path);
            httpRequest.Method = HttpMethod.Post;
            httpRequest.Content = new PushStreamContent(async stream =>
            {
                requestStreamTcs.SetResult(stream);
                await requestStreamCompleteTcs.Task;
            });

            Stopwatch stopwatch = Stopwatch.StartNew();
            Console.WriteLine("Starting HTTP/2 call - " + httpRequest.RequestUri);
            HttpClient client = new HttpClient(handler);
            var responseMessageTask = client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            await SendRequestContent(requestStreamTcs, requestStreamCompleteTcs, stopwatch);

            var responseMessage = await responseMessageTask;

            Console.WriteLine($"Headers received - {responseMessage.Headers.Count()} headers  - " + stopwatch.Elapsed.TotalSeconds);
            foreach (var header in responseMessage.Headers)
            {
                Console.WriteLine($"Header - {header.Key} = {header.Value.SingleOrDefault()}");
            }
            Console.WriteLine($"Status code - {responseMessage.StatusCode} - {stopwatch.Elapsed.TotalSeconds}");

            var receiveContentTask = Task.Run(async () =>
            {
                var contentStream = await responseMessage.Content.ReadAsStreamAsync();
                var buffer = new byte[2048];
                var readContent = 0;
                while ((readContent = await contentStream.ReadAsync(buffer)) > 0)
                {
                    Console.WriteLine($"Content received - {readContent} bytes - {stopwatch.Elapsed.TotalSeconds}");
                }
                Console.WriteLine($"Finished receiving content - {stopwatch.Elapsed.TotalSeconds}");
            });

            await receiveContentTask;
            Console.WriteLine($"Finished call - {stopwatch.Elapsed.TotalSeconds}");
            Console.WriteLine();
        }

        private static async Task SendRequestContent(TaskCompletionSource<Stream> requestStreamTcs, TaskCompletionSource<bool> requestStreamCompleteTcs, Stopwatch stopwatch)
        {
            var requestStream = await requestStreamTcs.Task;

            Console.WriteLine($"Sending message 1 - {stopwatch.Elapsed.TotalSeconds}");
            await requestStream.WriteAsync(Encoding.UTF8.GetBytes("Hello world 1"));

            await Task.Delay(TimeSpan.FromSeconds(10));

            Console.WriteLine($"Sending message 2 - {stopwatch.Elapsed.TotalSeconds}");
            await requestStream.WriteAsync(Encoding.UTF8.GetBytes("Hello world 2"));

            Console.WriteLine($"Finished sending content - {stopwatch.Elapsed.TotalSeconds}");
            requestStreamCompleteTcs.SetResult(true);
        }

        public class PushStreamContent : HttpContent
        {
            private readonly Func<Stream, Task> _onStreamAvailable;

            public PushStreamContent(Func<Stream, Task> onStreamAvailable)
            {
                _onStreamAvailable = onStreamAvailable;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                await _onStreamAvailable(stream);
            }

            protected override bool TryComputeLength(out long length)
            {
                // We can't know the length of the content being pushed to the output stream.
                length = -1;
                return false;
            }
        }
    }
}
