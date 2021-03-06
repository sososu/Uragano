﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Uragano.Abstractions;

namespace Uragano.Remoting
{
    public class Client : IClient
    {
        private IChannel Channel { get; }
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IServiceResult>> _resultCallbackTask =
            new ConcurrentDictionary<string, TaskCompletionSource<IServiceResult>>();

        private IMessageListener MessageListener { get; }

        private IEventLoopGroup EventLoopGroup { get; }

        private ILogger Logger { get; }

        public Client(IChannel channel, IEventLoopGroup eventLoopGroup, IMessageListener messageListener, ILogger logger)
        {
            Channel = channel;
            MessageListener = messageListener;
            MessageListener.OnReceived += MessageListener_OnReceived;
            EventLoopGroup = eventLoopGroup;
            Logger = logger;
        }

        private void MessageListener_OnReceived(TransportMessage<IServiceResult> message)
        {
            if (_resultCallbackTask.TryGetValue(message.Id, out var task))
            {
                task.TrySetResult(message.Body);
            }
            else
                Logger.LogError($"Not found callback[message id:{message.Id}]");
        }

        public async Task<IServiceResult> SendAsync(IInvokeMessage message)
        {
            var transportMessage = new TransportMessage<IInvokeMessage>
            {
                Id = Guid.NewGuid().ToString("N"),
                Body = message
            };
            if (Logger.IsEnabled(LogLevel.Debug))
                Logger.LogTrace($"Sending message.[message id:{transportMessage.Id}]");
            var tcs = new TaskCompletionSource<IServiceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var ct = new CancellationTokenSource(UraganoOptions.Remoting_Invoke_CancellationTokenSource_Timeout.Value))
            {
                ct.Token.Register(() => { tcs.TrySetResult(new ServiceResult("Remoting invoke timeout!", RemotingStatus.Timeout)); }, false);
                if (!_resultCallbackTask.TryAdd(transportMessage.Id, tcs)) throw new Exception("Failed to send.");
                try
                {
                    await Channel.WriteAndFlushAsync(transportMessage);
                    if (Logger.IsEnabled(LogLevel.Debug))
                        Logger.LogTrace($"Send completed, waiting for results.[message id:{transportMessage.Id}]");
                    return await tcs.Task;
                }
                finally
                {
                    _resultCallbackTask.TryRemove(transportMessage.Id, out var t);
                    t.TrySetCanceled();
                }
            }
        }

        public async Task DisconnectAsync()
        {
            Logger.LogTrace($"Stopping dotnetty client.[{Channel.LocalAddress}]");
            foreach (var task in _resultCallbackTask.Values)
            {
                task.TrySetCanceled();
            }

            _resultCallbackTask.Clear();
            if (Channel.Open)
            {
                await Channel.CloseAsync();
                await EventLoopGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }

            Logger.LogTrace($"The dotnetty client[{Channel.LocalAddress}] has stopped.");
        }
    }
}
