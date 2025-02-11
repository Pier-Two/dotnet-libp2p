// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethermind.Libp2p.Stack;
using Nethermind.Libp2p.Core;
using System.Text;
using System.Text.Json;
using Nethermind.Libp2p.Protocols.Pubsub;
using Multiformats.Address.Protocols;
using Multiformats.Address;
using Nethermind.Libp2p.Protocols;

ServiceProvider serviceProvider = new ServiceCollection()
    .AddLibp2p(builder => builder)
    .AddLogging(builder =>
        builder.SetMinimumLevel(args.Contains("--trace") ? LogLevel.Trace : LogLevel.Information)
            .AddSimpleConsole(l =>
            {
                l.SingleLine = true;
                l.TimestampFormat = "[HH:mm:ss.FFF]";
            }))
    .BuildServiceProvider();

IPeerFactory peerFactory = serviceProvider.GetService<IPeerFactory>()!;

ILogger logger = serviceProvider.GetService<ILoggerFactory>()!.CreateLogger("Pubsub Chat");
CancellationTokenSource ts = new();

Identity localPeerIdentity = new();
string addr = $"/ip4/0.0.0.0/tcp/0/p2p/{localPeerIdentity.PeerId}";

ILocalPeer peer = peerFactory.Create(localPeerIdentity, Multiaddress.Decode(addr));

PubsubRouter router = serviceProvider.GetService<PubsubRouter>()!;
ITopic topic = router.GetTopic("chat-room:awesome-chat-room");
topic.OnMessage += (byte[] msg) =>
{
    try
    {
        ChatMessage? chatMessage = JsonSerializer.Deserialize<ChatMessage>(Encoding.UTF8.GetString(msg));

        if (chatMessage is not null)
        {
            Console.WriteLine("{0}: {1}", chatMessage.SenderNick, chatMessage.Message);
        }
    }
    catch
    {
        Console.Error.WriteLine("Enable to decode chat message");
    }
};

await peer.ListenAsync(addr, ts.Token);

_ = serviceProvider.GetService<MDnsDiscoveryProtocol>()!.DiscoverAsync(peer.Address, token: ts.Token);

_ = router.RunAsync(peer, token: ts.Token);

string peerId = peer.Address.Get<P2P>().ToString();

string nickName = "libp2p-dotnet";

while (true)
{
    string msg = Console.ReadLine()!;
    if (msg == "exit")
    {
        break;
    }
    if (string.IsNullOrWhiteSpace(msg))
    {
        continue;
    }
    topic.Publish(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new ChatMessage(msg, peerId, nickName))));
}

ts.Cancel();

record class ChatMessage(string Message, string SenderId, string SenderNick);
