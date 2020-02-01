using System;
using System.Threading;
using System.Threading.Tasks;
using Ignitor;
using Microsoft.AspNetCore.SignalR.Client;

namespace test
{
    class Program
    {
        const string ServerUrl = "https://localhost:5001";
        static async Task Main(string[] args)
        {
            var client = new BlazorClient();
            await client.ConnectAsync(new Uri(ServerUrl));

            var links = new[] { "counter", "fetchdata", "home" };
            for (var i = 0; i < 12; i++)
            {
                var link = links[i % links.Length];
                var batch = await client.ExpectRenderBatch(() => client.NavigateAsync(ServerUrl + "/" + link));
                if (!client.Hive.TryFindElementById(link, out _))
                {
                    throw new InvalidOperationException($"Expected to have navigated to {link}.");
                }

                Console.WriteLine($"Navigated to {link}.");
                await Task.Delay(500);
            }
        }
    }

    static class BlazorClientExtensions
    {
        public static Task NavigateAsync(this BlazorClient client, string url, CancellationToken cancellationToken = default)
        {
            return client.HubConnection.InvokeAsync("OnLocationChanged", url, false, cancellationToken);
        }
    }
}
