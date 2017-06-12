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
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            var installedPackagesConfig = XDocument.Load(HostingEnvironment.MapPath("~/App_Data/packages/installed/installedPackages.config"));
            var installedLibraries = installedPackagesConfig.Descendants("file").Where(f => f.Value.ToLower().EndsWith("dll")).Select(f => f.Value.Substring(5)).Distinct();
            var configuration = Configuration.GetConfiguration() ?? new Configuration {Folder = "Libraries"};

            //create backup folder if it doesn't exist
            if (Directory.Exists(HostingEnvironment.MapPath("~/" + configuration.Folder)) == false)
            {
                try
                {
                    LogHelper.Info<Backup>("Creating backup folder");

                    Directory.CreateDirectory(HostingEnvironment.MapPath("~/" + configuration.Folder));
                }
                catch (Exception ex)
                {
                    LogHelper.Error<Backup>("Failed to create backup folder", ex);

                    throw;
                }
            }

            //delete existing backups that are no longer required and remove from configuration
            foreach (var library in Directory.GetFiles(HostingEnvironment.MapPath("~/" + configuration.Folder)))
            {
                LogHelper.Info<Backup>("Deleting expired backups");

                var fileName = Path.GetFileName(library);

                if (installedLibraries.Contains(fileName) == false)
                {
                    LogHelper.Info<Backup>(string.Format("Removing {0}", fileName));

                    File.Delete(library);

                    configuration.Libraries.Remove(fileName);
                }
            }

            //iterate over all libraries
            foreach (var library in installedLibraries)
            {
                //check if library exists in bin folder
                if (File.Exists(HostingEnvironment.MapPath("~/bin/" + library)))
                {
                    //copy library to backup folder if it doesn't exist
                    if (File.Exists(HostingEnvironment.MapPath("~/" + configuration.Folder + "/" + library)) == false)
                    {
                        LogHelper.Info<Backup>(string.Format("Backing up {0}", library));

                        //copy library to backup location
                        File.Copy(HostingEnvironment.MapPath("~/bin/" + library), HostingEnvironment.MapPath("~/" + configuration.Folder + "/" + library), true);
                    }

                    //add to configuration list if it doesn't exist
                    if (configuration.Libraries.Contains(library) == false)
                    {
                        LogHelper.Info<Backup>(string.Format("Adding {0} to configuration", library));

                        configuration.Libraries.Add(library);
                    }
                }
                else
                {
                    LogHelper.Info<Backup>(string.Format("Restoring {0}", library));

                    //restore library from backup location
                    File.Copy(HostingEnvironment.MapPath("~/" + configuration.Folder + "/" + library), HostingEnvironment.MapPath("~/bin/" + library), true);

                    //execute packages if specified
                    if (configuration.ExecutePackages)
                    {
                        LogHelper.Info<Backup>(string.Format("Restoring {0}", library));

                        //load plugin assembly
                        var plugin = System.Reflection.Assembly.LoadFile(HostingEnvironment.MapPath("~/bin/" + library));

                        //create instances of all objects that implement IPackageAction
                        var instances = plugin.GetTypes().Where(mytype => mytype.GetInterfaces().Contains(typeof(IPackageAction))).Select(t => Activator.CreateInstance(t) as IPackageAction);

                        //iterate over all instances and execute 
                        foreach (var instance in instances)
                        {
                            try
                            {
                                LogHelper.Info<Backup>(string.Format("Executing package actions {0}", instance.Alias()));

                                instance.Execute(instance.Alias(), instance.SampleXml());
                            }
                            catch (Exception ex)
                            {
                                LogHelper.Error<Backup>("Failed to execute package", ex);
                            }
                        }
                    }
                }
            }

            //save configuration
            configuration.Save();
        }
    }
}