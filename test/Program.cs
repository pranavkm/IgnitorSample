using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ignitor;
using Microsoft.AspNetCore.SignalR.Client;

namespace test
{
    class Program
    {
        public static readonly string ServerUrl = "https://localhost:5001";

        static async Task Main(string[] args)
        {
            var client = new BlazorClient();
            client.JSInterop += async (args) =>
            {
                if (args.Identifier == "Blazor._internal.navigationManager.navigateTo")
                {
                    var jsonDocument = JsonDocument.Parse(args.ArgsJson);
                    var array = jsonDocument.RootElement;
                    var uri = array[0].GetString();
                    if (array[1].GetBoolean())
                    {
                        throw new NotSupportedException("Force load is not supported.");
                    }
                    
                    await client.NavigateAsync(ServerUrl + "/" + uri);
                }
                
            };
            await client.ConnectAsync(new Uri(ServerUrl));

            await Navigate(client);

            await Clicks(client);

            await NavigateOnClick(client);

            Console.WriteLine("Done");
        }


        static async Task Clicks(BlazorClient client)
        {
            var batch = await client.ExpectRenderBatch(() => client.NavigateAsync(ServerUrl + "/home"));
            if (!client.Hive.TryFindElementById("changeState", out var changeState))
            {
                throw new InvalidOperationException($"Expected to have navigated to the home page.");
            }

            client.Hive.TryFindElementById("state", out var state);

            await client.ExpectRenderBatch(() => changeState.ClickAsync(client.HubConnection));

            if (state.Attributes["data-state"].ToString() == "Clicked")
            {
                Console.WriteLine("State changed to clicked.");
            }
            else
            {
                throw new InvalidOperationException("State was not 'Clicked'.");
            }

            await client.ExpectRenderBatch(() => changeState.DoubleClickAsync(client.HubConnection));

            if (state.Attributes["data-state"].ToString() == "DoubleClicked")
            {
                Console.WriteLine("State changed to dblclicked.");
            }
            else
            {
                throw new InvalidOperationException("State was not 'DoubleClicked'.");
            }
        }

        static async Task Navigate(BlazorClient client)
        {
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

        static async Task NavigateOnClick(BlazorClient client)
        {
            var batch = await client.ExpectRenderBatch(() => client.NavigateAsync(ServerUrl + "/home"));
            if (!client.Hive.TryFindElementById("navigateOnClick", out var navigateOnClick))
            {
                throw new InvalidOperationException($"Expected to have navigated to the home page.");
            }

            await navigateOnClick.ClickAsync(client.HubConnection);
            // Wait for one or more renders that causes fetchdata to have been updated.
            await client.ExistsAsync("fetchdata");
        }
    }

    static class BlazorClientExtensions
    {
        public static Task NavigateAsync(this BlazorClient client, string url, CancellationToken cancellationToken = default)
        {
            return client.HubConnection.InvokeAsync("OnLocationChanged", url, false, cancellationToken);
        }

        public static async Task ExistsAsync(this BlazorClient client, string id, TimeSpan? timeout = default)
        {
            timeout ??= TimeSpan.FromSeconds(3);
            var cts = new CancellationTokenSource(timeout.Value);

            while (!cts.IsCancellationRequested)
            {
                if (client.Hive.TryFindElementById(id, out _))
                {
                    return;
                }

                await client.PrepareForNextBatch(timeout);
            }

            throw new TimeoutException($"Unable to find element with id {id} in {timeout.Value} duration.");
        }
    }

    static class ElementNodeExtensions
    {
        static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };


        public static Task DoubleClickAsync(this ElementNode node, HubConnection connection, CancellationToken cancellationToken = default)
        {
            if (!node.Events.TryGetValue("dblclick", out var clickEventDescriptor))
            {
                throw new InvalidOperationException("Element does not have a click event.");
            }

            var descriptor = new
            {
                BrowserRendererId = 0,
                EventHandlerId = clickEventDescriptor.EventId,
                EventArgsType = "mouse",
            };

            var mouseEventArgs = new
            {
                Type = clickEventDescriptor.EventName,
                Detail = 1
            };

            return connection.InvokeAsync(
                "DispatchBrowserEvent",
                JsonSerializer.Serialize(descriptor, jsonSerializerOptions),
                JsonSerializer.Serialize(mouseEventArgs, jsonSerializerOptions));
        }
    }
}
