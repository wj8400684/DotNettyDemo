﻿using System.Security.Cryptography.X509Certificates;
using DotNetty.Codecs;
using DotNetty.Handlers.Logging;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Echo.Server;
using Examples.Common;

ExampleHelper.SetConsoleLogger();

var bossGroup = new MultithreadEventLoopGroup(1);
var workerGroup = new MultithreadEventLoopGroup();

X509Certificate2? tlsCertificate = null;
if (ServerSettings.IsSsl)
{
    tlsCertificate = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
}

try
{
    var bootstrap = new ServerBootstrap();
    bootstrap.Group(bossGroup, workerGroup);

    bootstrap.Channel<TcpServerSocketChannel>();

    bootstrap
        .Option(ChannelOption.SoBacklog, 100)
        .Handler(new LoggingHandler("SRV-LSTN"))
        .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
        {
            IChannelPipeline pipeline = channel.Pipeline;
            if (tlsCertificate != null)
            {
                pipeline.AddLast("tls", TlsHandler.Server(tlsCertificate));
            }

            pipeline.AddLast(new LoggingHandler("SRV-CONN"));
            pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
            pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));

            pipeline.AddLast("echo", new EchoServerHandler());
        }));

    IChannel boundChannel = await bootstrap.BindAsync(ServerSettings.Port);

    Console.ReadLine();

    await boundChannel.CloseAsync();
}
finally
{
    await Task.WhenAll(
        bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
        workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
}