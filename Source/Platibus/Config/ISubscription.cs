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

namespace Platibus.Config
{
    /// <summary>
    /// An interface describing a subscription to a topic hosted on a local or
    /// remote bus instance
    /// </summary>
    public interface ISubscription
    {
        /// <summary>
        /// The name of the publisher endpoint
        /// </summary>
        EndpointName Endpoint { get; }

        /// <summary>
        /// The name of the topic
        /// </summary>
        TopicName Topic { get; }

        /// <summary>
        /// The Time-To-Live (TTL) for the subscription
        /// </summary>
        /// <remarks>
        /// Subscriptions will regularly be renewed, but the TTL serves as a
        /// "dead man's switch" that will cause the subscription to be terminated
        /// if not renewed within that span of time.
        /// </remarks>
        TimeSpan TTL { get; }
    }
}