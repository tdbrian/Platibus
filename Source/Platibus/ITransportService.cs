﻿// The MIT License (MIT)
// 
// Copyright (c) 2015 Jesse Sweetland
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
using System.Threading;
using System.Threading.Tasks;

namespace Platibus
{
    /// <summary>
    /// An interface that describes an object that transmits messages from one
    /// application to another.
    /// </summary>
    public interface ITransportService
    {
        /// <summary>
        /// Sends a message directly to the application identified by the
        /// <see cref="IMessageHeaders.Destination"/> header.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="credentials">The credentials required to send a 
        /// message to the specified destination, if applicable.</param>
        /// <param name="cancellationToken">A token used by the caller to
        /// indicate if and when the send operation has been canceled.</param>
        /// <returns>returns a task that completes when the message has
        /// been successfully sent to the destination.</returns> 
        Task SendMessage(Message message, IEndpointCredentials credentials = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Publishes a message to a topic.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        /// <param name="topicName">The name of the topic.</param>
        /// <param name="cancellationToken">A token used by the caller
        /// to indicate if and when the publish operation has been canceled.</param>
        /// <returns>returns a task that completes when the message has
        /// been successfully published to the topic.</returns>
        Task PublishMessage(Message message, TopicName topicName, CancellationToken cancellationToken);

        /// <summary>
        /// Subscribes to messages published to the specified <paramref name="topicName"/>
        /// by the application at the provided <paramref name="publisherUri"/>.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="topicName">The name of the topic to which the caller is
        ///     subscribing.</param>
        /// <param name="ttl">(Optional) The Time To Live (TTL) for the subscription
        ///     on the publishing application if it is not renewed.</param>
        /// <param name="cancellationToken">A token used by the caller to
        ///     indicate if and when the subscription should be canceled.</param>
        /// <returns>Returns a long-running task that will be completed when the 
        /// subscription is canceled by the caller or a non-recoverable error occurs.</returns>
        Task Subscribe(IEndpoint endpoint, TopicName topicName, TimeSpan ttl, CancellationToken cancellationToken = default(CancellationToken));
    }
}