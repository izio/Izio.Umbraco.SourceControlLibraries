using System;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using System.Xml.Linq;
using umbraco.interfaces;
using Umbraco.Core;
using Umbraco.Core.Logging;

namespace Izio.Umbraco.SourceControlLibraries
{
    public class Backup : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication,
            ApplicationContext applicationContext)
        {
            var installedPackagesConfig = XDocument.Load(HostingEnvironment.MapPath("~/App_Data/packages/installed/installedPackages.config"));
            var installedLibraries = installedPackagesConfig.Descendants("file").Where(f => f.Value.ToLower().EndsWith("dll")).Select(f => f.Value.Substring(5)).Distinct();
            var configuration = Configuration.GetConfiguration() ?? new Configuration {Folder = "Libraries"};

            //create backup folder if it doesn't exist
            if (Directory.Exists(HostingEnvironment.MapPath("~/" + configuration.Folder)) == false)
            {
                Directory.CreateDirectory(HostingEnvironment.MapPath("~/" + configuration.Folder));
            }

            //delete existing backups that are no longer required and remove from configuration
            foreach (var library in Directory.GetFiles(HostingEnvironment.MapPath("~/" + configuration.Folder)))
            {
                var fileName = Path.GetFileName(library);

                if (configuration.Libraries.Contains(fileName) == false)
                {
                    File.Delete(library);

                    configuration.Libraries.Remove(fileName);
                }
            }

            //iterate over all libraries
            foreach (var library in installedLibraries)
            {
                if (File.Exists(HostingEnvironment.MapPath("~/bin/" + library)))
                {
                    //copy library to backup location
                    File.Copy(HostingEnvironment.MapPath("~/bin/" + library), HostingEnvironment.MapPath("~/" + configuration.Folder + "/" + library), true);
                }
                else
                {
                    //restore library from backup location
                    File.Copy(HostingEnvironment.MapPath("~/" + configuration.Folder + "/" + library), HostingEnvironment.MapPath("~/bin/" + library), true);

                    //execute packages if specified
                    if (configuration.ExecutePackages)
                    {
                        //load plugin assembly
                        var plugin = System.Reflection.Assembly.LoadFile(HostingEnvironment.MapPath("~/bin/" + library));

                        //create instances of all objects that implement IPackageAction
                        var instances = plugin.GetTypes().Where(mytype => mytype.GetInterfaces().Contains(typeof(IPackageAction))).Select(t => Activator.CreateInstance(t) as IPackageAction);

                        //iterate over all instances and execute 
                        foreach (var instance in instances)
                        {
                            try
                            {
                                instance.Execute(instance.Alias(), instance.SampleXml());
                            }
                            catch (Exception ex)
                            {
                                LogHelper.Error<Backup>("Failed to execute package", ex);
                            }
                        }
                    }
                }

                if (configuration.Libraries.Contains(library) == false)
                {
                    configuration.Libraries.Add(library);
                }
            }

            //save configuration
            configuration.Save();
        }
    }
}