﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using DotNetty.Codecs;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Examples.Common;
using Telnet.Client;

ExampleHelper.SetConsoleLogger();

var group = new MultithreadEventLoopGroup();

X509Certificate2? cert = null;
string? targetHost = null;
if (ClientSettings.IsSsl)
{
    cert = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
    targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
}

try
{
    var bootstrap = new Bootstrap();
    bootstrap
        .Group(group)
        .Channel<TcpSocketChannel>()
        .Option(ChannelOption.TcpNodelay, true)
        .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
        {
            IChannelPipeline pipeline = channel.Pipeline;

            if (cert != null)
            {
                pipeline.AddLast(new TlsHandler(
                    stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true),
                    new ClientTlsSettings(targetHost)));
            }

            pipeline.AddLast(new DelimiterBasedFrameDecoder(8192, Delimiters.LineDelimiter()));
            pipeline.AddLast(new StringEncoder(), new StringDecoder(), new TelnetClientHandler());
        }));

    IChannel bootstrapChannel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

    for (;;)
    {
        string line = Console.ReadLine();
        if (string.IsNullOrEmpty(line))
        {
            continue;
        }

        try
        {
            await bootstrapChannel.WriteAndFlushAsync(line + "\r\n");
        }
        catch
        {
        }

        if (string.Equals(line, "bye", StringComparison.OrdinalIgnoreCase))
        {
            await bootstrapChannel.CloseAsync();
            break;
        }
    }

    await bootstrapChannel.CloseAsync();
}
finally
{
    group.ShutdownGracefullyAsync().Wait(1000);
}