﻿// The MIT License (MIT)
// 
// Copyright (c) 2014 Jesse Sweetland
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Platibus.Config;
using Platibus.Security;
using Platibus.Serialization;

namespace Platibus
{
    public class Bus : IBus, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(LoggingCategories.Core);

        private bool _disposed;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly IEndpointCollection _endpoints;
        private readonly IList<Task> _subscriptionTasks = new List<Task>();
        private readonly IList<IHandlingRule> _handlingRules;
        private readonly IMessageNamingService _messageNamingService;
        private readonly IMessageJournalingService _messageJournalingService;
        private readonly IMessageQueueingService _messageQueueingService;
        
        private readonly MemoryCacheReplyHub _replyHub = new MemoryCacheReplyHub(TimeSpan.FromMinutes(5));
        private readonly IList<ISendRule> _sendRules;
        private readonly ISerializationService _serializationService;
        private readonly IList<ISubscription> _subscriptions;
        private readonly IList<TopicName> _topics;
        private readonly Uri _baseUri;
        private readonly ITransportService _transportService;

        public Bus(IPlatibusConfiguration configuration, Uri baseUri, ITransportService transportService, IMessageQueueingService messageQueueingService)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (baseUri == null) throw new ArgumentNullException("baseUri");
            if (transportService == null) throw new ArgumentNullException("transportService");
            if (messageQueueingService == null) throw new ArgumentNullException("messageQueueingService");

            _baseUri = baseUri;
            _transportService = transportService;
            _messageQueueingService = messageQueueingService;

            // TODO: Throw configuration exception if message queueing service, message naming
            // service, or serialization service are null

            _messageJournalingService = configuration.MessageJournalingService;
            _messageNamingService = configuration.MessageNamingService;
            _serializationService = configuration.SerializationService;

            _endpoints = new ReadOnlyEndpointCollection(configuration.Endpoints);
            _topics = configuration.Topics.ToList();
            _sendRules = configuration.SendRules.ToList();
            _handlingRules = configuration.HandlingRules.ToList();
            _subscriptions = configuration.Subscriptions.ToList();
        }

        public async Task Init(CancellationToken cancellationToken = default(CancellationToken))
        {
            var handlingRulesGroupedByQueueName = _handlingRules
                .GroupBy(r => r.QueueName)
                .ToDictionary(grp => grp.Key, grp => grp);

            foreach (var ruleGroup in handlingRulesGroupedByQueueName)
            {
                var queueName = ruleGroup.Key;
                var rules = ruleGroup.Value;
                var handlers = rules.Select(r => r.MessageHandler);

                var queueListener = new MessageHandlingListener(this, _messageNamingService, _serializationService, handlers);
                await _messageQueueingService.CreateQueue(queueName, queueListener, cancellationToken: cancellationToken);
            }

            foreach (var subscription in _subscriptions)
            {
                var endpoint = _endpoints[subscription.Publisher];

                // The returned task will no complete until the subscription is
                // canceled via the supplied cancelation token, so we shouldn't
                // await it.

                var subscriptionTask = _transportService.Subscribe(endpoint, subscription.Topic, subscription.TTL,
                    _cancellationTokenSource.Token);

                _subscriptionTasks.Add(subscriptionTask);
            }
        }

        public async Task<ISentMessage> Send(object content, SendOptions options = default(SendOptions),
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckDisposed();

            if (content == null) throw new ArgumentNullException("content");

            var prototypicalMessage = BuildMessage(content, options: options);
            var endpoints = GetEndpointsForMessage(prototypicalMessage);
            var transportTasks = new List<Task>();

            // Create the sent message before transporting it in order to ensure that the
            // reply stream is cached before any replies arrive.
            var sentMessage = _replyHub.CreateSentMessage(prototypicalMessage);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var kvp in endpoints)
            {
                var endpointName = kvp.Key;
                var endpoint = kvp.Value;
                var credentials = endpoint.Credentials;
                var perEndpointHeaders = new MessageHeaders(prototypicalMessage.Headers)
                {
                    Destination = endpoint.Address
                };

                Log.DebugFormat("Sending message ID {0} to endpoint \"{1}\" ({2})...",
                    prototypicalMessage.Headers.MessageId, endpointName, endpoint.Address);

                var addressedMessage = new Message(perEndpointHeaders, prototypicalMessage.Content);
                transportTasks.Add(_transportService.SendMessage(addressedMessage, credentials, cancellationToken));
            }
            await Task.WhenAll(transportTasks);
            return sentMessage;
        }

        public async Task<ISentMessage> Send(object content, EndpointName endpointName,
            SendOptions options = default(SendOptions), CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckDisposed();

            if (content == null) throw new ArgumentNullException("content");
            if (endpointName == null) throw new ArgumentNullException("endpointName");

            var endpoint = _endpoints[endpointName];
            var credentials = endpoint.Credentials;
            var headers = new MessageHeaders
            {
                Destination = endpoint.Address
            };
            var message = BuildMessage(content, headers, options);

            Log.DebugFormat("Sending message ID {0} to endpoint \"{1}\" ({2})...",
                message.Headers.MessageId, endpointName, endpoint.Address);

            // Create the sent message before transporting it in order to ensure that the
            // reply stream is cached before any replies arrive.
            var sentMessage = _replyHub.CreateSentMessage(message);
            await _transportService.SendMessage(message, credentials, cancellationToken);
            return sentMessage;
        }

        public async Task<ISentMessage> Send(object content, Uri endpointUri, IEndpointCredentials credentials = null,
            SendOptions options = default(SendOptions),
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckDisposed();

            if (content == null) throw new ArgumentNullException("content");
            if (endpointUri == null) throw new ArgumentNullException("endpointUri");

            var headers = new MessageHeaders
            {
                Destination = endpointUri
            };

            var message = BuildMessage(content, headers, options);

            Log.DebugFormat("Sending message ID {0} to \"{2}\"...",
                message.Headers.MessageId, endpointUri);

            // Create the sent message before transporting it in order to ensure that the
            // reply stream is cached before any replies arrive.
            var sentMessage = _replyHub.CreateSentMessage(message);
            await _transportService.SendMessage(message, credentials, cancellationToken);
            return sentMessage;
        }

        public async Task Publish(object content, TopicName topic,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckDisposed();
            if (!_topics.Contains(topic)) throw new TopicNotFoundException(topic);

            var prototypicalHeaders = new MessageHeaders
            {
                Topic = topic,
                Published = DateTime.UtcNow
            };

            var message = BuildMessage(content, prototypicalHeaders);
            if (_messageJournalingService != null)
            {
                await _messageJournalingService.MessagePublished(message, cancellationToken);
            }

            await _transportService.PublishMessage(message, topic, cancellationToken);
        }

        private Message BuildMessage(object content, IMessageHeaders suppliedHeaders = null,
            SendOptions options = default(SendOptions))
        {
            if (content == null) throw new ArgumentNullException("content");
            var messageName = _messageNamingService.GetNameForType(content.GetType());
            var headers = new MessageHeaders(suppliedHeaders)
            {
                MessageId = MessageId.Generate(),
                MessageName = messageName,
                Origination = _baseUri,
                Importance = options.Importance
            };

            var contentType = options.ContentType;
            if (string.IsNullOrWhiteSpace(contentType))
            {
                contentType = "application/json";
            }
            headers.ContentType = contentType;

            var serializer = _serializationService.GetSerializer(headers.ContentType);
            var serializedContent = serializer.Serialize(content);
            return new Message(headers, serializedContent);
        }

        private IEnumerable<KeyValuePair<EndpointName, IEndpoint>> GetEndpointsForMessage(Message message)
        {
            return _sendRules
                .Where(r => r.Specification.IsSatisfiedBy(message))
                .SelectMany(r => r.Endpoints)
                .Join(_endpoints, n => n, d => d.Key, (n, d) => new {Name = n, Endpoint = d.Value})
                .ToDictionary(x => x.Name, x => x.Endpoint);
        }

        internal async Task SendReply(BusMessageContext messageContext, object replyContent,
            SendOptions options = default(SendOptions), CancellationToken cancellationToken = default(CancellationToken))
        {
            if (messageContext == null) throw new ArgumentNullException("messageContext");
            if (replyContent == null) throw new ArgumentNullException("replyContent");

            IEndpoint replyToEndpoint;
            IEndpointCredentials credentials = null;
            var replyTo = messageContext.Headers.ReplyTo ?? messageContext.Headers.Origination;
            if (_endpoints.TryGetEndpointByUri(replyTo, out replyToEndpoint))
            {
                credentials = replyToEndpoint.Credentials;
            }

            var headers = new MessageHeaders
            {
                Destination = messageContext.Headers.Origination,
                RelatedTo = messageContext.Headers.MessageId
            };
            var replyMessage = BuildMessage(replyContent, headers, options);
            await _transportService.SendMessage(replyMessage, credentials, cancellationToken);
        }

        public async Task HandleMessage(Message message, IPrincipal principal)
        {
            if (_messageJournalingService != null)
            {
                await _messageJournalingService.MessageReceived(message);
            }

            // Make sure that the principal is serializable before enqueuing
            var senderPrincipal = principal as SenderPrincipal;
            if (senderPrincipal == null && principal != null)
            {
                senderPrincipal = new SenderPrincipal(principal);
            }

            var tasks = new List<Task>();

            var relatedToMessageId = message.Headers.RelatedTo;
            var isReply = relatedToMessageId != default(MessageId);
            if (isReply)
            {
                tasks.Add(NotifyReplyReceived(message));
            }

            var importance = message.Headers.Importance;
            if (importance.RequiresQueueing)
            {
                // Message expiration handled in MessageHandlingListener
                tasks.AddRange(_handlingRules
                    .Where(r => r.MessageSpecification.IsSatisfiedBy(message))
                    .Select(rule => rule.QueueName)
                    .Distinct()
                    .Select(q => _messageQueueingService.EnqueueMessage(q, message, senderPrincipal)));
            }
            else
            {
                tasks.Add(HandleMessageImmediately(message, senderPrincipal, isReply));
            }

            await Task.WhenAll(tasks);
        }

        private async Task HandleMessageImmediately(Message message, SenderPrincipal senderPrincipal, bool isReply)
        {
            var messageContext = new BusMessageContext(this, message.Headers, senderPrincipal);
            var handlers = _handlingRules
                .Where(r => r.MessageSpecification.IsSatisfiedBy(message))
                .Select(rule => rule.MessageHandler)
                .ToList();

            if (!handlers.Any() && isReply)
            {
                // TODO: Figure out what to do here.
                return;
            }

            await MessageHandler.HandleMessage(_messageNamingService, _serializationService,
                handlers, message, messageContext, _cancellationTokenSource.Token);

            if (!messageContext.MessageAcknowledged)
            {
                throw new MessageNotAcknowledgedException();
            }
        }

        private async Task NotifyReplyReceived(Message message)
        {
            // TODO: Handle special "last reply" message.  Presently we only support a single reply.
            // This will probably be a special function of the ITransportService and will likely
            // be, for example, an empty POST to {baseUri}/message/{messageId}?lastReply=true, which
            // will trigger the OnComplete event in the IObservable reply stream.  However, we need
            // to put some thought into potential issues around the sequence and timing of replies
            // vs. the final lastReply POST to ensure that all replies are processed before the
            // OnComplete event is triggered.  One possibility is a reply sequence number header
            // and an indication of the total number of replies in the lastReply POST.  If the
            // number of replies received is less than the number expected, then the OnComplete
            // event can be deferred.

            var relatedToMessageId = message.Headers.RelatedTo;
            var messageType = _messageNamingService.GetTypeForName(message.Headers.MessageName);
            var serializer = _serializationService.GetSerializer(message.Headers.ContentType);
            var messageContent = serializer.Deserialize(message.Content, messageType);

            await _replyHub.ReplyReceived(messageContent, relatedToMessageId);
            await _replyHub.NotifyLastReplyReceived(relatedToMessageId);
        }

        private void CheckDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        ~Bus()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Dispose(true);
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _cancellationTokenSource.Cancel();
            Task.WhenAll(_subscriptionTasks).TryWait(TimeSpan.FromSeconds(30));
            if (disposing)
            {
                _cancellationTokenSource.Dispose();
                _replyHub.Dispose();
            }
        }
    }
}