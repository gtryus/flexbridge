﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Chorus.FileTypeHanders;
using Chorus.merge;
using Chorus.merge.xml.generic;
using Chorus.VcsDrivers.Mercurial;
using Palaso.IO;

namespace FLEx_ChorusPlugin.Infrastructure.Handling.CustomProperties
{
	internal sealed class CustomPropertiesTypeHandlerStrategy : IFieldWorksFileHandler
	{
		#region Implementation of IFieldWorksFileHandler

		private const string CustomField = "CustomField";
		private const string Key = "key";

		public bool CanValidateFile(string pathToFile)
		{
			return FileUtils.CheckValidPathname(pathToFile, SharedConstants.CustomProperties) &&
				   Path.GetFileName(pathToFile) == SharedConstants.CustomPropertiesFilename;
		}

		public string ValidateFile(string pathToFile)
		{
			try
			{
				var doc = XDocument.Load(pathToFile);
				var root = doc.Root;
				if (root.Name.LocalName != SharedConstants.AdditionalFieldsTag)
					return "Not valid custom properties file";
				if (!root.HasElements)
					return null; // CustomFields are optional.

				var requiredAttrs = new HashSet<string>
										{
											"name",
											"class",
											"type",
											Key // Special attr added so fast xml splitter can find each one.
										};
				var optionalAttrs = new HashSet<string>
										{
											"destclass",
											"wsSelector",
											"helpString",
											"listRoot",
											"label"
										};
				foreach (var customFieldElement in root.Elements(CustomField))
				{
					if (requiredAttrs
						.Any(requiredAttr => customFieldElement.Attribute(requiredAttr) == null))
					{
						return "Missing required custom property attribute";
					}
					if (customFieldElement.Attributes()
						.Any(attribute => !requiredAttrs.Contains(attribute.Name.LocalName)
							&& !optionalAttrs.Contains(attribute.Name.LocalName)))
					{
						return "Contains unrecognized attribute";
					}
					// Make sure 'key' attr is class+name.
					if (customFieldElement.Attribute("class").Value + customFieldElement.Attribute("name").Value != customFieldElement.Attribute(Key).Value)
						return "Mis-matched 'key' attribute with property class+name atributes";

					if (customFieldElement.HasElements)
						return "Contains illegal child element";
				}

				MetadataCache.MdCache.AddCustomPropInfo(new MergeOrder(
					pathToFile, pathToFile, pathToFile,
					new MergeSituation(pathToFile, "", "", "", "", MergeOrder.ConflictHandlingModeChoices.WeWin)));

				return null;
			}
			catch (Exception e)
			{
				return e.Message;
			}
		}

		public IChangePresenter GetChangePresenter(IChangeReport report, HgRepository repository)
		{
			return FieldWorksChangePresenter.GetCommonChangePresenter(report, repository);
		}

		public IEnumerable<IChangeReport> Find2WayDifferences(FileInRevision parent, FileInRevision child, HgRepository repository)
		{
			return Xml2WayDiffService.ReportDifferences(repository, parent, child,
				null,
				CustomField, Key);
		}

		public void Do3WayMerge(MetadataCache mdc, MergeOrder mergeOrder)
		{
			// NB: Doesn't need the mdc updated with custom props.
			XmlMergeService.Do3WayMerge(mergeOrder,
				new FieldWorksCustomPropertyMergingStrategy(mergeOrder.MergeSituation),
				true,
				null,
				CustomField, Key);
		}

		public string Extension
		{
			get { return SharedConstants.CustomProperties; }
		}

		#endregion
	}
}