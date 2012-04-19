﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FLEx_ChorusPlugin.Infrastructure;
using FLEx_ChorusPlugin.Infrastructure.DomainServices;

namespace FLEx_ChorusPlugin.Contexts.Linguistics.TextCorpus
{
	/// <summary>
	/// Read/Write the text corpus bounded context.
	///
	/// The Text instances owned in the Texts property of lang proj, including all they own, need to then be removed from 'classData',
	/// as that stuff will be stored elsewhere.
	///
	/// Each Text instance will be in its own file, along with everything it owns (nested ownership as well).
	/// The folder pattern is:
	/// Linguistics\TextCorpus\foo.textincorpus, where foo is the guid of a Text.
	///
	/// Data that is common to all texts will be in the main Linguistics\TextCorpus folder,
	/// such as the "GenreList" property of Lang Proj.
	/// </summary>
	internal static class TextCorpusBoundedContextService
	{
		internal static void NestContext(string linguisticsBaseDir, IDictionary<string, SortedDictionary<string, XElement>> classData, Dictionary<string, string> guidToClassMapping)
		{
			var textCorpusBaseDir = Path.Combine(linguisticsBaseDir, SharedConstants.TextCorpus);
			if (!Directory.Exists(textCorpusBaseDir))
				Directory.CreateDirectory(textCorpusBaseDir);

			var langProjElement = classData["LangProject"].Values.First();

			// Write Genre list (owning atomic CmPossibilityList)
			FileWriterService.WriteNestedListFileIfItExists(classData, guidToClassMapping,
										  langProjElement, SharedConstants.GenreList,
										  Path.Combine(textCorpusBaseDir, SharedConstants.GenreListFilename));

			// Write text markup tags list (owning atomic CmPossibilityList)
			FileWriterService.WriteNestedListFileIfItExists(classData, guidToClassMapping,
										  langProjElement, SharedConstants.TextMarkupTags,
										  Path.Combine(textCorpusBaseDir, SharedConstants.TextMarkupTagsListFilename));

			// Handle the LP TranslationTags prop (OA-CmPossibilityList), if it exists.
			FileWriterService.WriteNestedListFileIfItExists(classData, guidToClassMapping,
										  langProjElement, SharedConstants.TranslationTags,
										  Path.Combine(textCorpusBaseDir, SharedConstants.TranslationTagsListFilename));

			var texts = classData["Text"];
			if (texts.Count == 0)
				return; // No texts to process, regardless of owner. (Some can be owned by RnGenericRec.)

			var textGuidsInLangProj = BaseDomainServices.GetGuids(langProjElement, "Texts");
			if (textGuidsInLangProj.Count == 0)
				return; //  None owned by lang project. (Some can be owned by RnGenericRec.)

			foreach (var textGuid in textGuidsInLangProj)
			{
				var rootElement = new XElement("TextInCorpus");
				var textElement = texts[textGuid];
				rootElement.Add(textElement);
				CmObjectNestingService.NestObject(
					false,
					textElement,
					new Dictionary<string, HashSet<string>>(),
					classData,
					guidToClassMapping);
				FileWriterService.WriteNestedFile(
					Path.Combine(textCorpusBaseDir, "Test_" + textGuid.ToLowerInvariant() + "." + SharedConstants.TextInCorpus),
					rootElement);
			}
			// Remove child objsur nodes from owning LangProg
			langProjElement.Element("Texts").RemoveNodes();
		}

		internal static void FlattenContext(
			SortedDictionary<string, XElement> highLevelData,
			SortedDictionary<string, XElement> sortedData,
			string linguisticsBaseDir)
		{
			var textCorpusBaseDir = Path.Combine(linguisticsBaseDir, SharedConstants.TextCorpus);
			if (!Directory.Exists(textCorpusBaseDir))
				return;

			var langProjElement = highLevelData["LangProject"];
			var langProjGuid = langProjElement.Attribute(SharedConstants.GuidStr).Value.ToLowerInvariant();

			// Put the Genre list back in the right place.
			var pathname = Path.Combine(textCorpusBaseDir, SharedConstants.GenreListFilename);
			var doc = XDocument.Load(pathname);
			BaseDomainServices.RestoreElement(pathname, sortedData, langProjElement, SharedConstants.GenreList, doc.Root.Element(SharedConstants.CmPossibilityList));
			// Put the markup tags list back in the right place.
			pathname = Path.Combine(textCorpusBaseDir, SharedConstants.TextMarkupTagsListFilename);
			doc = XDocument.Load(pathname);
			BaseDomainServices.RestoreElement(pathname, sortedData, langProjElement, SharedConstants.TextMarkupTags, doc.Root.Element(SharedConstants.CmPossibilityList));
			// Put the translation tags list back in the right place.
			pathname = Path.Combine(textCorpusBaseDir, SharedConstants.TranslationTagsListFilename);
			doc = XDocument.Load(pathname);
			BaseDomainServices.RestoreElement(pathname, sortedData, langProjElement, SharedConstants.TranslationTags, doc.Root.Element(SharedConstants.CmPossibilityList));

			// Put Texts back into LP.
			var sortedTexts = new SortedDictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
			foreach (var textPathname in Directory.GetFiles(textCorpusBaseDir, "*." + SharedConstants.TextInCorpus, SearchOption.TopDirectoryOnly))
			{
				var textDoc = XDocument.Load(textPathname);
				// Put texts back into index's Entries element.
				var root = textDoc.Root;
				var textElement = root.Elements().First();
				CmObjectFlatteningService.FlattenObject(
					textPathname,
					sortedData,
					textElement,
					langProjGuid); // Restore 'ownerguid' to text.
				var textGuid = textElement.Attribute(SharedConstants.GuidStr).Value.ToLowerInvariant();
				sortedTexts.Add(textGuid, BaseDomainServices.CreateObjSurElement(textGuid));
			}
			// Restore LP Texts property in sorted order.
			if (sortedTexts.Count == 0)
				return;
			var langProjOwningProp = langProjElement.Element("Texts");
			foreach (var sortedTextObjSurElement in sortedTexts.Values)
				langProjOwningProp.Add(sortedTextObjSurElement);
		}
	}
}