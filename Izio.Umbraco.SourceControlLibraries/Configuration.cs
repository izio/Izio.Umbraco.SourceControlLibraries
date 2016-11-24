using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Hosting;
using System.Xml.Serialization;
using Umbraco.Core.Logging;

namespace Izio.Umbraco.SourceControlLibraries
{
    [Serializable]
    public class Configuration
    {
        public string Folder { get; set; }

        public bool ExecutePackages { get; set; }

        public List<string> Libraries { get; set; }

        public Configuration()
        {
            Libraries = new List<string>();
        }

        public void Save()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Configuration));

                using (var writer = new StreamWriter(HostingEnvironment.MapPath("~/Config/SourceControlLibraries.config")))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error<Configuration>("Failed to save configuration file", ex);
            }
        }

        public static Configuration GetConfiguration()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Configuration));

                using (var reader = File.OpenText(HostingEnvironment.MapPath("~/Config/SourceControlLibraries.config")))
                {
                    return (Configuration)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
             {
                LogHelper.Error<Configuration>("Failed to load configuration file", ex);
            }

            return null;
        }
    }
}