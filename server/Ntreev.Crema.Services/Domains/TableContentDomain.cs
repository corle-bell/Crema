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
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml;
using Ntreev.Crema.Services.Properties;
using Ntreev.Crema.Services;
using Ntreev.Crema.ServiceModel;
using Ntreev.Crema.Data;
using Ntreev.Library.IO;
using Ntreev.Library.ObjectModel;
using Ntreev.Crema.Data.Xml;
using Ntreev.Crema.Data.Xml.Schema;
using Ntreev.Crema.Services.Users;
using System.Windows.Threading;
using Ntreev.Crema.Services.Data;
using Ntreev.Library.Serialization;
using System.Runtime.Serialization;
using Ntreev.Library;

namespace Ntreev.Crema.Services.Domains
{
    [Serializable]
    class TableContentDomain : Domain
    {
        public const string TypeName = "Table";
        private const string TablesRevisionKey = "tablesRevision";
        private const string TypesRevisionKey = "typesRevision";

        private CremaDataSet dataSet;
        private List<FindResultInfo> findResults = new List<FindResultInfo>(100);
        private Dictionary<string, DataView> views = new Dictionary<string, DataView>();

        private TableContentDomain(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public TableContentDomain(Authentication authentication, CremaDataSet dataSet, DataBase dataBase, string itemPath, string itemType)
            : base(authentication.ID, dataBase.ID, itemPath, itemType)
        {
            if (dataSet.HasChanges() == true)
                throw new ArgumentException(Resources.Exception_UnsavedDataCannotEdit, nameof(dataSet));
            this.dataSet = dataSet;

            foreach (var item in this.dataSet.Tables)
            {
                var view = item.AsDataView();
                this.views.Add(item.TableName, view);
            }
        }

        public override object Source
        {
            get { return this.dataSet; }
        }

        protected override byte[] SerializeSource()
        {
            var xml = XmlSerializerUtility.GetString(this.dataSet);
            return Encoding.UTF8.GetBytes(xml.Compress());
        }

        protected override void DerializeSource(byte[] data)
        {
            var xml = Encoding.UTF8.GetString(data).Decompress();
            this.dataSet = XmlSerializerUtility.ReadString<CremaDataSet>(xml);

            foreach (var item in this.dataSet.Tables)
            {
                var view = item.AsDataView();
                this.views.Add(item.TableName, view);
            }
        }

        protected override void OnSerializaing(SerializationInfo info, StreamingContext context)
        {
            base.OnSerializaing(info, context);

            info.AddValue(TablesRevisionKey, GetTablesRevisionXml());
            info.AddValue(TypesRevisionKey, GetTypesRevisionXml());

            string GetTablesRevisionXml()
            {
                var list = new List<SerializableKeyValuePair<string, long>>();
                if (this.Source is CremaDataSet dataSet)
                {
                    foreach (var table in dataSet.Tables)
                    {
                        list.Add(new SerializableKeyValuePair<string, long>(table.Name, table.Revision));
                    }
                }
                return XmlSerializerUtility.GetString(list);
            }

            string GetTypesRevisionXml()
            {
                var list = new List<SerializableKeyValuePair<string, long>>();
                if (this.Source is CremaDataSet dataSet)
                {
                    foreach (var type in dataSet.Types)
                    {
                        list.Add(new SerializableKeyValuePair<string, long>(type.Name, type.Revision));
                    }
                }
                return XmlSerializerUtility.GetString(list);
            }
        }

        protected override void Ondeserializing(SerializationInfo info)
        {
            base.Ondeserializing(info);

            var tablesRevisionXml = info.GetValue(TablesRevisionKey, typeof(string)) as string;
            var tablesRevision = XmlSerializerUtility.ReadString(tablesRevisionXml, typeof(List<SerializableKeyValuePair<string, long>>));
            if (tablesRevision != null && tablesRevision is List<SerializableKeyValuePair<string, long>> tables)
            {
                foreach (var table in tables)
                {
                    if (table.Key != null && this.dataSet.Tables.Contains(table.Key))
                    {
                        this.dataSet.Tables[table.Key].UpdateRevision(table.Value);
                    }
                }
            }

            var typesRevisionXml = info.GetValue(TypesRevisionKey, typeof(string)) as string;
            var typesRevision = XmlSerializerUtility.ReadString(typesRevisionXml, typeof(List<SerializableKeyValuePair<string, long>>));
            if (typesRevision != null && typesRevision is List<SerializableKeyValuePair<string, long>> types)
            {
                foreach (var type in types)
                {
                    if (type.Key != null && this.dataSet.Types.Contains(type.Key))
                    {
                        this.dataSet.Types[type.Key].UpdateRevision(type.Value);
                    }
                }
            }
        }

        protected override DomainRowInfo[] OnNewRow(DomainUser domainUser, DomainRowInfo[] rows, SignatureDateProvider signatureProvider)
        {
            this.dataSet.SignatureDateProvider = signatureProvider;

            try
            {
                for (var i = 0; i < rows.Length; i++)
                {
                    var view = this.views[rows[i].TableName];
                    var rowView = CremaDomainUtility.AddNew(view, rows[i].Fields);
                    rows[i].Keys = CremaDomainUtility.GetKeys(rowView);
                    rows[i].Fields = CremaDomainUtility.GetFields(rowView);
                }

                this.dataSet.AcceptChanges();

                return rows;
            }
            catch (Exception e)
            {
                this.CremaHost.Error(e);
                this.dataSet.RejectChanges();
                throw e;
            }
        }

        protected override DomainRowInfo[] OnSetRow(DomainUser domainUser, DomainRowInfo[] rows, SignatureDateProvider signatureProvider)
        {
            this.dataSet.SignatureDateProvider = signatureProvider;

            try
            {
                for (var i = 0; i < rows.Length; i++)
                {
                    var view = this.views[rows[i].TableName];
                    rows[i].Fields = CremaDomainUtility.SetFields(view, rows[i].Keys, rows[i].Fields);
                }

                this.dataSet.AcceptChanges();
                return rows;
            }
            catch (Exception e)
            {
                this.CremaHost.Error(e);
                this.dataSet.RejectChanges();
                throw e;
            }
        }

        protected override void OnRemoveRow(DomainUser domainUser, DomainRowInfo[] rows, SignatureDateProvider signatureProvider)
        {
            this.dataSet.SignatureDateProvider = signatureProvider;

            try
            {
                foreach (var item in rows)
                {
                    var view = this.views[item.TableName];
                    if (DomainRowInfo.ClearKey.SequenceEqual(item.Keys) == true)
                    {
                        view.Table.Clear();
                    }
                    else
                    {
                        CremaDomainUtility.Delete(view, item.Keys);
                    }
                }
                this.dataSet.AcceptChanges();
            }
            catch (Exception e)
            {
                this.CremaHost.Error(e);
                this.dataSet.RejectChanges();
                throw e;
            }
        }
    }
}
