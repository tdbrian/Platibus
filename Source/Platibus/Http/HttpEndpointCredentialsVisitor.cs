﻿using System;
using System.Net;
using System.Net.Http;
using Platibus.Security;

namespace Platibus.Http
{
    internal class HttpEndpointCredentialsVisitor : IEndpointCredentialsVisitor
    {
        private readonly HttpClientHandler _clientHandler;

        public HttpEndpointCredentialsVisitor(HttpClientHandler clientHandler)
        {
            if (clientHandler == null) throw new ArgumentNullException("clientHandler");
            _clientHandler = clientHandler;
        }

        public void Visit(BasicAuthCredentials credentials)
        {
            _clientHandler.Credentials = new NetworkCredential(credentials.Username, credentials.Password);
        }

        public void Visit(DefaultCredentials credentials)
        {
            _clientHandler.UseDefaultCredentials = true;
        }
    }
}