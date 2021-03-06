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
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Security.Principal;

namespace Platibus.Security
{
    [Serializable]
    public class SenderIdentity : IIdentity, ISerializable
    {
        private readonly string _name;
        private readonly string _authenticationType;
        private readonly bool _isAuthenticated;

        public string AuthenticationType
        {
            get { return _authenticationType; }
        }

        public bool IsAuthenticated
        {
            get { return _isAuthenticated; }
        }

        public string Name
        {
            get { return _name; }
        }

        public SenderIdentity(IIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException("identity");
            _name = identity.Name;
            _authenticationType = identity.AuthenticationType;
            _isAuthenticated = identity.IsAuthenticated;
        }

        protected SenderIdentity(SerializationInfo info, StreamingContext context)
        {
            _name = info.GetString("name");
            _authenticationType = info.GetString("authenticationType");
            _isAuthenticated = info.GetBoolean("isAuthenticated");
        }

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("name", _name);
            info.AddValue("authenticationType", _authenticationType);
            info.AddValue("isAuthenticated", _isAuthenticated);
        }

        public override string ToString()
        {
            return _name;
        }
    }
}