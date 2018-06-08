﻿using Ntreev.Crema.Data;
using Ntreev.Crema.Data.Xml;
using Ntreev.Crema.Data.Xml.Schema;
using Ntreev.Library;
using Ntreev.Library.IO;
using Ntreev.Library.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ntreev.Crema.Services
{
    [Export(typeof(IObjectSerializer))]
    class XmlObjectSerializer : IObjectSerializer
    {
        public string Name => "xml";

        public object Deserialize(string itemPath, Type type, IDictionary properties)
        {
            if (type == typeof(CremaDataTable))
            {
                throw new NotImplementedException();
            }
            else if (type == typeof(CremaDataType))
            {
                throw new NotImplementedException();
            }
            else if (type == typeof(CremaDataSet))
            {
                if (properties is CremaDataSetPropertyCollection props)
                {
                    var dataSet = CremaDataSet.Create(props.SignatureDateProvider);
                    var typePaths = props.TypePaths.Select(item => item + CremaSchema.SchemaExtension).ToArray();
                    var tablePaths = props.TablePaths.Select(item => item + CremaSchema.XmlExtension).ToArray();

                    dataSet.ReadMany(typePaths, tablePaths, props.SchemaOnly);
                    dataSet.AcceptChanges();

                    return dataSet;
                }
                else
                {
                    return CremaDataSet.ReadFromDirectory(itemPath);
                }
            }
            else
            {
                var filename = itemPath + CremaSchema.XmlExtension;
                return DataContractSerializerUtility.Read(filename, type);
            }
        }

        public string[] Serialize(string itemPath, object obj, IDictionary properties)
        {
            if (obj is CremaDataTable dataTable)
            {
                return this.SerializeDataTable(dataTable, itemPath, properties);
            }
            else if (obj is CremaDataType dataType)
            {
                return this.SerializeDataType(dataType, itemPath, properties);
            }
            else if (obj is CremaDataSet dataSet)
            {
                dataSet.WriteToDirectory(itemPath);
                var items1 = DirectoryUtility.GetAllFiles(itemPath, "*.xml");
                var items2 = DirectoryUtility.GetAllFiles(itemPath, "*.xsd");
                return items1.Concat(items2).OrderBy(item => item).ToArray();
            }
            else
            {
                return this.SerializeObject(obj, itemPath);
            }
        }

        public string[] GetPath(string itemPath, Type type, IDictionary properties)
        {
            if (type == typeof(CremaDataTable))
            {
                var xmlPath = itemPath + CremaSchema.XmlExtension;
                if (properties is RelativeSchemaPropertyCollection prop && prop.RelativePath != string.Empty)
                {
                    var uri = new Uri(xmlPath);
                    var schemaUri = UriUtility.Combine(UriUtility.GetDirectoryName(uri), prop.RelativePath);
                    var schemaPath = schemaUri.LocalPath + CremaSchema.SchemaExtension;
                    return new string[] { xmlPath, schemaPath };
                }
                else
                {
                    if (File.Exists(xmlPath) == true)
                    {
                        var xmlInfo = new CremaXmlReadInfo(xmlPath);
                        return new string[] { xmlPath, xmlInfo.SchemaPath, };
                    }
                    else
                    {
                        var schemaPath = itemPath + CremaSchema.SchemaExtension;
                        return new string[] { xmlPath, schemaPath, };
                    }
                }
            }
            else if (type == typeof(CremaDataType))
            {
                var filename = itemPath + CremaSchema.SchemaExtension;
                return new string[] { filename };
            }
            else
            {
                var filename = itemPath + CremaSchema.XmlExtension;
                return new string[] { filename };
            }
        }

        public string[] GetReferencedPath(string itemPath, Type type, IDictionary properties)
        {
            if (type == typeof(CremaDataTable))
            {
                var xmlPath = itemPath + CremaSchema.XmlExtension;
                var xmlInfo = new CremaXmlReadInfo(xmlPath);
                var schemaInfo = new CremaSchemaReadInfo(xmlInfo.SchemaPath);
                return schemaInfo.LocalTypePaths;
            }
            else if (type == typeof(CremaDataType))
            {
                return new string[] { };
            }
            else
            {
                return new string[] { };
            }
        }

        public string[] GetItemPaths(string path, Type type, IDictionary properties)
        {
            if (type == typeof(CremaDataTable))
            {
                return DirectoryUtility.GetAllFiles(path, "*" + CremaSchema.XmlExtension).Select(item => FileUtility.RemoveExtension(item)).ToArray();
            }
            else if (type == typeof(CremaDataType))
            {
                return DirectoryUtility.GetAllFiles(path, "*" + CremaSchema.SchemaExtension).Select(item => FileUtility.RemoveExtension(item)).ToArray();
            }
            else
            {
                var directories = DirectoryUtility.GetAllDirectories(path, "*", true);
                var files = DirectoryUtility.GetAllFiles(path, "*.xml").Select(item => FileUtility.RemoveExtension(item));
                return directories.Concat(files).OrderBy(item => item).ToArray();
            }
        }

        public static readonly XmlObjectSerializer Default = new XmlObjectSerializer();

        private string[] SerializeDataTable(CremaDataTable dataTable, string itemPath, IDictionary properties)
        {
            var xmlPath = itemPath + CremaSchema.XmlExtension;

            if (properties is RelativeSchemaPropertyCollection prop && prop.RelativePath != string.Empty)
            {
                File.WriteAllText(xmlPath, dataTable.GetXml(), Encoding.UTF8);
                return new string[] { xmlPath };
            }
            else
            {
                var schemaPath = itemPath + CremaSchema.SchemaExtension;
                File.WriteAllText(schemaPath, dataTable.GetXmlSchema(), Encoding.UTF8);
                File.WriteAllText(xmlPath, dataTable.GetXml(), Encoding.UTF8);
                
                return new string[] { xmlPath, schemaPath };
            }
        }

        private string[] SerializeDataType(CremaDataType dataType, string itemPath, object state)
        {
            var schemaPath = itemPath + CremaSchema.SchemaExtension;
            File.WriteAllText(schemaPath, dataType.GetXmlSchema(), Encoding.UTF8);
            return new string[] { schemaPath, };
        }

        private string[] SerializeObject(object obj, string itemPath)
        {
            var filename = itemPath + CremaSchema.XmlExtension;
            DataContractSerializerUtility.Write(filename, obj, true);
            return new string[] { filename };
        }
    }
}