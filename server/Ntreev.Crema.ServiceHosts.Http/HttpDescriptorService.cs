﻿//Released under the MIT License.
//
//Copyright (c) 2018 Ntreev Soft co., Ltd.
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
//documentation files (the "Software"), to deal in the Software without restriction, including without limitation the 
//rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit 
//persons to whom the Software is furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the 
//Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE 
//WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR 
//COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR 
//OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Ntreev.Crema.ServiceHosts.Http.Extensions;
using Ntreev.Crema.ServiceHosts.Http.Responses.DescriptorService;
using Ntreev.Crema.ServiceModel;
using Ntreev.Crema.Services;
using Ntreev.Library;

namespace Ntreev.Crema.ServiceHosts.Http
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, IncludeExceptionDetailInFaults = true)]
    class HttpDescriptorService : IHttpDescriptorService
    {
        private readonly CremaService service;
        private readonly ICremaHost cremaHost;

        public HttpDescriptorService(CremaService service, ICremaHost cremaHost)
        {
            this.service = service;
            this.cremaHost = cremaHost;
        }

        public Message GetServiceInfos()
        {
            return this.service.ServiceInfos.ToJsonMessage();
        }

        public Message GetDataBaseInfos()
        {
            return this.cremaHost.Dispatcher.Invoke(() => this.cremaHost.DataBases.Select(item => item.DataBaseInfo).ToArray()).ToJsonMessage();
        }

        public Message GetVersion()
        {
            return new VersionResponse
            {
                Version = AppUtility.ProductVersion
            }.ToJsonMessage();
        }

        public Message IsOnline(string userID, string password)
        {
            var userContext = this.cremaHost.GetService(typeof(IUserContext)) as IUserContext;
            return new IsOnlineResponse
            {
                IsOnline = userContext.Dispatcher.Invoke(() => userContext.IsOnlineUser(userID, password.ToSecureString()))
            }.ToJsonMessage();
        }
    }
}
