using System;
using Sitecore.Azure.Pipelines.BasePipeline;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Sitecore.Azure.Pipelines.CreateAzurePackage;
using Sitecore.Azure.Pipelines.CreateAzurePackage.Base;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.IO;
using Sitecore.Reflection;


namespace Sitecore.Support.Azure.Pipelines.CreateAzurePackage.Azure
{
  public class ConfigurationChanges : Sitecore.Azure.Pipelines.CreateAzurePackage.Azure.ConfigurationChanges
  {
    protected override void Action([CanBeNull] RolePipelineArgsBase arguments)
    {
      var args = arguments as CreateAzureDeploymentPipelineArgs;
      Assert.IsNotNull(args, "args");
      string sourceWebConfigPath = FileUtil.MakePath(args.TargetWebSiteFolder.FullName, "web.config", '\\');
      args.WebConfig = XDocument.Load(sourceWebConfigPath);

      XElement sitecoreConfigSource = this.SitecoreConfigSectionManager.GetSitecoreSection(args.WebConfig);

      if (sitecoreConfigSource != null)
      {
        string configPath = this.SitecoreConfigSectionManager.GetSectionConfigSource(sitecoreConfigSource);

        if (!string.IsNullOrWhiteSpace(configPath))
        {
          args.SitecoreSectionConfigSource = configPath;

          XDocument sitecoreConfig = XDocument.Load(FileUtil.MakePath(args.TargetWebSiteFolder.FullName, configPath));

          sitecoreConfigSource.ReplaceWith(sitecoreConfig.Root);
        }
      }

      this.ExpandIncludes(args);

      ReflectionUtil.CallMethod(typeof(ConfigurationChangesBase), this, "SetDatabaseDefinitionsToPatchFile", true, true, false, new object[] { args.Deployment.ConnectionStringsPatch, args });
      ReflectionUtil.CallMethod(typeof(ConfigurationChangesBase), this, "SetDatabaseDefinitionsToPatchFile", true, true, false, new object[] { args.Deployment.GlobalWebConfigPatch, args });

      args.WebConfig = this.Patch(args.WebConfig, args.Deployment.GlobalWebConfigPatch);
      args.WebConfig = this.Patch(args.WebConfig, args.Deployment.DeploymentTypeWebConfigPatch);
      args.WebConfig = this.Patch(args.WebConfig, args.Deployment.CustomWebConfigPatch);

      var connectionStrings = new XDocument(this.SitecoreConfigSectionManager.CreateConnectionStringsSection());
      args.ConnectionString = this.Patch(connectionStrings, args.Deployment.ConnectionStringsPatch);
    }

    private void ExpandIncludes([NotNull] CreateAzureDeploymentPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      XElement sitecoreSection = this.SitecoreConfigSectionManager.GetSitecoreSection(args.WebConfig);
      IEnumerable<XElement> list = sitecoreSection.Descendants().Where(i => i.Name.ToString().ToLower() == "sc.include");

      foreach (XElement node in list)
      {
        XAttribute fileAttr = node.Attributes().FirstOrDefault(i => i.Name.LocalName.ToLower() == "file");
        if (fileAttr != null)
        {
          string path = fileAttr.Value;
          if (FileUtil.IsFullyQualified(path))
          {
            string mappedPath = FileUtil.MapPath(path).ToLower();
            string relativeFolder = mappedPath.Replace(args.WebSiteFolder.FullName.ToLower(), string.Empty);

            fileAttr.Value = FileUtil.MakePath(args.TargetWebSiteFolder.FullName.ToLower(), relativeFolder, '\\');
          }
        }
      }

      XElement expandedSitecoreSection = this.GetExpandedConfig(sitecoreSection, args.SourceIncludeDir);

      foreach (XElement node in list)
      {
        XAttribute fileAttr = node.Attributes().FirstOrDefault(i => i.Name.LocalName.ToLower() == "file");
        if (fileAttr != null)
        {
          string path = fileAttr.Value;
          if (FileUtil.IsFullyQualified(path))
          {
            FileUtil.Delete(path);
          }
        }
      }

      args.SourceIncludeDir.Delete(true);

      sitecoreSection.ReplaceWith(expandedSitecoreSection);
    }

    [NotNull]
    private XElement GetExpandedConfig([NotNull] XElement root, [NotNull] DirectoryInfo includeDir)
    {
      Assert.ArgumentNotNull(root, "root");
      Assert.ArgumentNotNull(includeDir, "includeDir");

      var doc = new XmlDocument();
      using (XmlReader xmlReader = root.CreateReader())
      {
        doc.Load(xmlReader);
      }

      XmlNode xmlDoc = doc.DocumentElement;
      Assert.IsNotNull(xmlDoc, "xmlDoc");

      var ciclyc = new Hashtable();
      var configReader = new ConfigReader();

      ReflectionUtil.CallMethod(configReader.GetType(), configReader, "ExpandIncludeFiles", true, true, false, new object[] { xmlDoc, ciclyc });

      Type type = ReflectionUtil.GetTypeInfo("Sitecore.Configuration.ConfigPatcher, Sitecore.Kernel");
      ConstructorInfo constructor = type.GetConstructors()[0];
      object patcher = constructor.Invoke(new object[] { xmlDoc });

      ReflectionUtil.CallMethod(configReader.GetType(), configReader, "LoadAutoIncludeFiles", true, true, false, new[] { patcher, includeDir.FullName });

      using (var nodeReader = new XmlNodeReader(xmlDoc))
      {
        nodeReader.MoveToContent();

        return XElement.Load(nodeReader);
      }
    }
  }
}