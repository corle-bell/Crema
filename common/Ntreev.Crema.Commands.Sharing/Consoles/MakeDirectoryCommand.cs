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

using Ntreev.Crema.ServiceModel;
using Ntreev.Crema.Services;
using Ntreev.Library.Commands;
using Ntreev.Library.IO;
using Ntreev.Library.ObjectModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;

namespace Ntreev.Crema.Commands.Consoles
{
    [Export(typeof(IConsoleCommand))]
    [ResourceDescription("Resources", IsShared = true)]
    class MakeDirectoryCommand : ConsoleCommandBase
    {
        public MakeDirectoryCommand()
            : base("mkdir")
        {

        }

        [CommandProperty(IsRequired = true)]
        public string Path
        {
            get; set;
        }

        public override bool IsEnabled => this.CommandContext.IsOnline;

        protected override void OnExecute()
        {
            var path = this.CommandContext.GetAbsolutePath(this.Path);
            this.MakeDirectory(path);
        }

        private void MakeDirectory(string path)
        {
            var drive = this.CommandContext.GetDrive(path);
            if (drive == null)
                throw new ArgumentException(string.Format(Ntreev.Library.Properties.Resources.Exception_InvalidPath_Format, path), nameof(path));
            var absolutePath = this.CommandContext.GetAbsolutePath(path);
            var authentication = this.CommandContext.GetAuthentication(this);
            if (NameValidator.VerifyCategoryPath(absolutePath))
            {
                var categoryName = new CategoryName(absolutePath);
                drive.Create(authentication, categoryName.ParentPath, categoryName.Name);
            }
            else
            {
                var itemName = new ItemName(absolutePath);
                drive.Create(authentication, itemName.CategoryPath, itemName.Name);
            }
        }
    }
}
