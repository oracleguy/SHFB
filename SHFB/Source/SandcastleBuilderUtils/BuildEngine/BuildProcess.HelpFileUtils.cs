//===============================================================================================================
// System  : Sandcastle Help File Builder Utilities
// File    : BuildProcess.HelpFileUtils.cs
// Author  : Eric Woodruff  (Eric@EWoodruff.us)
// Updated : 04/09/2014
// Note    : Copyright 2006-2014, Eric Woodruff, All rights reserved
// Compiler: Microsoft Visual C#
//
// This file contains the code used to modify the help file project files to create a better table of contents
// and find the default help file page
//
// This code is published under the Microsoft Public License (Ms-PL).  A copy of the license should be
// distributed with the code.  It can also be found at the project website: https://GitHub.com/EWSoftware/SHFB.  This
// notice, the author's name, and all copyright notices must remain intact in all applications, documentation,
// and source files.
//
// Version     Date     Who  Comments
// ==============================================================================================================
// 1.0.0.0  08/07/2006  EFW  Created the code
// 1.2.0.0  09/06/2006  EFW  Added support for TOC content placement
// 1.3.0.0  09/09/2006  EFW  Added support for website output
// 1.3.1.0  10/02/2006  EFW  Added support for the September CTP
// 1.3.2.0  11/04/2006  EFW  Added support for the NamingMethod property
// 1.5.0.0  06/19/2007  EFW  Various additions and updates for the June CTP
// 1.5.2.0  09/13/2007  EFW  Added support for calling plug-ins
// 1.6.0.5  02/04/2008  EFW  Adjusted loading of Help 1 TOC to use an encoding based on the chosen language
// 1.6.0.7  04/12/2007  EFW  Added support for a split table of contents
// 1.9.0.0  06/06/2010  EFW  Added support for multi-format build output
// 1.9.0.0  06/30/2010  EFW  Reworked TOC handling to support parenting of API content to a conceptual topic for
//                           all formats.
// 1.9.4.0  02/19/2012  EFW  Added support for PHP website files.  Merged changes for VS2010 style from Don Fehr.
// 1.9.6.0  10/25/2012  EFW  Updated to use the new presentation style definition files
// 1.9.8.0  06/21/2013  EFW  Added support for format-specific help content files.  Removed
//                           ModifyHelpTopicFilenames() as naming is now handled entirely by AddFilenames.xsl.
// -------  01/09/2013  EFW  Removed copying of branding files.  They are part of the presentation style now.
//===============================================================================================================

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Web;

using Sandcastle.Core;

using SandcastleBuilder.Utils.BuildComponent;
using SandcastleBuilder.Utils.ConceptualContent;

namespace SandcastleBuilder.Utils.BuildEngine
{
    partial class BuildProcess
    {
        #region Private data members
        //=====================================================================

        // Branding manifest property constants
        private const string ManifestPropertyHelpOutput = "helpOutput";
        private const string ManifestPropertyPreBranding = "preBranding";
        private const string ManifestPropertyNoTransforms = "noTransforms";
        private const string ManifestPropertyOnlyTransforms = "onlyTransforms";
        private const string ManifestPropertyOnlyIcons = "onlyIcons";

        #endregion

        #region Table of content helper methods
        //=====================================================================

        /// <summary>
        /// This is used to determine the best placement for the API content based on the project settings
        /// </summary>
        private void DetermineApiContentPlacement()
        {
            TocEntryCollection parentCollection;
            TocEntry apiInsertionPoint, parentTopic;
            XmlDocument tocXml;
            int tocOrder = project.TocOrder;

            this.ApiTocParentId = null;
            this.ApiTocOrder = -1;

            tocXml = new XmlDocument();
            tocXml.Load(workingFolder + "toc.xml");

            XmlNodeList topics = tocXml.SelectNodes("topics/topic");

            // Note that in all cases, we only have to set the order on the item where it changes.
            // The sort order on all subsequent items will increment from there.
            if(toc != null && toc.Count != 0)
            {
                toc.ResetSortOrder();

                // See if a root content container is defined for MSHV output
                var root = conceptualContent.Topics.FirstOrDefault(t => t.MSHVRootContentContainer != null);

                if(root != null)
                    this.RootContentContainerId = root.MSHVRootContentContainer.Id;

                if(tocOrder == -1 || root != null)
                    tocOrder = 0;

                // If building MS Help Viewer output, ensure that the root container ID is valid
                // and not visible in the TOC if defined.
                if((project.HelpFileFormat & HelpFileFormats.MSHelpViewer) != 0 &&
                  !String.IsNullOrEmpty(this.RootContentContainerId) && toc[this.RootContentContainerId] != null)
                    throw new BuilderException("BE0069", String.Format(CultureInfo.CurrentCulture,
                        "The project's root content container topic (ID={0}) must be have its Visible property " +
                        "set to False in the content layout file.", this.RootContentContainerId));

                // Was an insertion point defined in the content layout?
                apiInsertionPoint = toc.ApiContentInsertionPoint;

                if(apiInsertionPoint != null)
                {
                    parentCollection = toc.ApiContentParentCollection;
                    toc[0].SortOrder = tocOrder;

                    // Insert the API content before, after, or as a child of a conceptual topic
                    switch(apiInsertionPoint.ApiParentMode)
                    {
                        case ApiParentMode.InsertBefore:
                            parentTopic = toc.ApiContentParent;

                            if(parentTopic == null)
                            {
                                this.ApiTocParentId = "**Root**";
                                this.ApiTocOrder = tocOrder + parentCollection.IndexOf(apiInsertionPoint);
                            }
                            else
                            {
                                this.ApiTocParentId = toc.ApiContentParent.Id;
                                this.ApiTocOrder = parentCollection.IndexOf(apiInsertionPoint);
                            }

                            apiInsertionPoint.SortOrder = this.ApiTocOrder + topics.Count;
                            break;

                        case ApiParentMode.InsertAfter:
                            parentTopic = toc.ApiContentParent;
                            this.ApiTocOrder = parentCollection.IndexOf(apiInsertionPoint) + 1;

                            if(parentTopic == null)
                            {
                                this.ApiTocParentId = "**Root**";

                                if(this.ApiTocOrder < parentCollection.Count)
                                    parentCollection[this.ApiTocOrder].SortOrder = tocOrder + this.ApiTocOrder + topics.Count;

                                this.ApiTocOrder += tocOrder;
                            }
                            else
                            {
                                this.ApiTocParentId = parentTopic.Id;

                                if(this.ApiTocOrder < parentCollection.Count)
                                    parentCollection[this.ApiTocOrder].SortOrder = this.ApiTocOrder + topics.Count;
                            }
                            break;

                        case ApiParentMode.InsertAsChild:
                            this.ApiTocParentId = apiInsertionPoint.Id;
                            this.ApiTocOrder = parentCollection.Count;
                            break;

                        default:    // Unknown
                            break;
                    }
                }
                else
                {
                    // Base the API sort order on the ContentPlacement property
                    if(project.ContentPlacement == ContentPlacement.AboveNamespaces)
                    {
                        toc[0].SortOrder = tocOrder;
                        this.ApiTocOrder = tocOrder + toc.Count;
                    }
                    else
                    {
                        this.ApiTocOrder = tocOrder;
                        toc[0].SortOrder = tocOrder + topics.Count;
                    }
                }
            }
            else
                this.ApiTocOrder = project.TocOrder;

            // Set the sort order on the first API topic if defined
            if(this.ApiTocOrder != -1 && topics.Count != 0)
            {
                XmlAttribute attr = tocXml.CreateAttribute("sortOrder");
                attr.Value = this.ApiTocOrder.ToString(CultureInfo.InvariantCulture);
                topics[0].Attributes.Append(attr);

                tocXml.Save(workingFolder + "toc.xml");
            }
        }

        /// <summary>
        /// This combines the conceptual and API intermediate TOC files into one file ready for transformation to
        /// the help format-specific TOC file formats.
        /// </summary>
        private void CombineIntermediateTocFiles()
        {
            XmlAttribute attr;
            XmlDocument conceptualXml = null, tocXml;
            XmlElement docElement;
            XmlNodeList allNodes;
            XmlNode node, parent;
            bool wasModified = false;
            int insertionPoint;

            this.ReportProgress(BuildStep.CombiningIntermediateTocFiles,
                "Combining conceptual and API intermediate TOC files...");

            if(this.ExecutePlugIns(ExecutionBehaviors.InsteadOf))
                return;

            this.ExecutePlugIns(ExecutionBehaviors.Before);

            // Load the TOC files
            if(toc != null && toc.Count != 0)
            {
                conceptualXml = new XmlDocument();
                conceptualXml.Load(workingFolder + "_ConceptualTOC_.xml");
            }

            tocXml = new XmlDocument();
            tocXml.Load(workingFolder + "toc.xml");

            // Merge the conceptual and API TOCs into one?
            if(conceptualXml != null)
            {
                // Remove the root content container if present as we don't need it for the other formats
                if((project.HelpFileFormat & HelpFileFormats.MSHelpViewer) != 0 &&
                  !String.IsNullOrEmpty(this.RootContentContainerId))
                {
                    docElement = conceptualXml.DocumentElement;
                    node = docElement.FirstChild;
                    allNodes = node.SelectNodes("topic");

                    foreach(XmlNode n in allNodes)
                    {
                        n.ParentNode.RemoveChild(n);
                        docElement.AppendChild(n);
                    }

                    node.ParentNode.RemoveChild(node);
                }

                if(String.IsNullOrEmpty(this.ApiTocParentId))
                {
                    // If not parented, the API content is placed above or below the conceptual content based on
                    // the project's ContentPlacement setting.
                    if(project.ContentPlacement == ContentPlacement.AboveNamespaces)
                    {
                        docElement = conceptualXml.DocumentElement;

                        foreach(XmlNode n in tocXml.SelectNodes("topics/topic"))
                        {
                            node = conceptualXml.ImportNode(n, true);
                            docElement.AppendChild(node);
                        }

                        tocXml = conceptualXml;
                    }
                    else
                    {
                        docElement = tocXml.DocumentElement;

                        foreach(XmlNode n in conceptualXml.SelectNodes("topics/topic"))
                        {
                            node = tocXml.ImportNode(n, true);
                            docElement.AppendChild(node);
                        }
                    }
                }
                else
                {
                    // Parent the API content to a conceptual topic
                    parent = conceptualXml.SelectSingleNode("//topic[@id='" + this.ApiTocParentId + "']");

                    // If not found, parent it to the root
                    if(parent == null)
                        parent = conceptualXml.DocumentElement;

                    insertionPoint = this.ApiTocOrder;

                    if(insertionPoint == -1 || insertionPoint >= parent.ChildNodes.Count)
                        insertionPoint = parent.ChildNodes.Count;

                    foreach(XmlNode n in tocXml.SelectNodes("topics/topic"))
                    {
                        node = conceptualXml.ImportNode(n, true);

                        if(insertionPoint >= parent.ChildNodes.Count)
                            parent.AppendChild(node);
                        else
                            parent.InsertBefore(node, parent.ChildNodes[insertionPoint]);

                        insertionPoint++;
                    }

                    tocXml = conceptualXml;
                }

                // Fix up empty container nodes by removing the file attribute and setting the ID attribute to
                // the title attribute value.
                foreach(XmlNode n in tocXml.SelectNodes("//topic[@title]"))
                {
                    attr = n.Attributes["file"];

                    if(attr != null)
                        n.Attributes.Remove(attr);

                    attr = n.Attributes["id"];

                    if(attr != null)
                        attr.Value = n.Attributes["title"].Value;
                }

                wasModified = true;
            }

            // Determine the default topic for Help 1 and website output if one was not specified in a site map
            // or content layout file.
            if(defaultTopic == null && (project.HelpFileFormat & (HelpFileFormats.HtmlHelp1 |
              HelpFileFormats.Website)) != 0)
            {
                node = tocXml.SelectSingleNode("topics/topic");

                if(node != null)
                {
                    if(node.Attributes["file"] != null)
                        defaultTopic = node.Attributes["file"].Value + ".htm?";
                    else
                    {
                        // Find the first node with a topic file, it may be nested
                        while(node.FirstChild != null && node.FirstChild.Attributes["file"] == null)
                            node = node.FirstChild;

                        if(node.FirstChild != null && node.FirstChild.Attributes["file"] != null)
                            defaultTopic = node.FirstChild.Attributes["file"].Value + ".htm?";
                    }

                    if(defaultTopic != null)
                    {
                        // Find the file
                        defaultTopic = Directory.EnumerateFiles(workingFolder + "Output", defaultTopic,
                          SearchOption.AllDirectories).FirstOrDefault();

                        if(defaultTopic != null)
                        {
                            defaultTopic = defaultTopic.Substring(workingFolder.Length + 7);

                            if(defaultTopic.IndexOf('\\') != -1)
                                defaultTopic = defaultTopic.Substring(defaultTopic.IndexOf('\\') + 1);
                        }
                    }
                }

                if(defaultTopic == null)
                    throw new BuilderException("BE0026", "Unable to determine default topic in " +
                        "toc.xml.  You may need to mark one as the default topic manually.");
            }

            if(wasModified)
                tocXml.Save(workingFolder + "toc.xml");

            this.ExecutePlugIns(ExecutionBehaviors.After);
        }
        #endregion

        #region Content copying methods
        //=====================================================================

        /// <summary>
        /// This is called to copy the standard content files (icons, scripts, style sheets, and other standard
        /// presentation style content) to the help output folders.
        /// </summary>
        /// <remarks>This creates the base folder <b>Output\</b>, one folder for each help file format, and an
        /// <b>.\html</b> folder under each of those.  It then copies the stock icon, script, and style sheet
        /// files from the defined presentation style help content folders.</remarks>
        private void CopyStandardHelpContent()
        {
            int idx = 0;

            this.ReportProgress(BuildStep.CopyStandardHelpContent, "Copying standard help content...");

            if(this.ExecutePlugIns(ExecutionBehaviors.InsteadOf))
                return;

            this.ExecutePlugIns(ExecutionBehaviors.Before);
            this.EnsureOutputFoldersExist("html");

            foreach(HelpFileFormats value in Enum.GetValues(typeof(HelpFileFormats)))
                if((project.HelpFileFormat & value) != 0)
                {
                    // EnsureOutputFoldersExist adds the folders to HelpFormatOutputFolders in the same order as
                    // the values so we can index it here.
                    presentationStyle.CopyHelpContent(value, this.HelpFormatOutputFolders[idx],
                        this.ReportProgress, (name, source, dest) => this.TransformTemplate(name, source, dest));
                    idx++;
                }

            this.ExecutePlugIns(ExecutionBehaviors.After);
        }

        /// <summary>
        /// This copies files from the specified source folder to the specified destination folder.  If any
        /// subfolders are found below the source folder and the wildcard is "*.*", the subfolders are also
        /// copied recursively.
        /// </summary>
        /// <param name="sourcePath">The source path from which to copy</param>
        /// <param name="destPath">The destination path to which to copy</param>
        /// <param name="verbose">True to list all files copied, false to display file counts instead</param>
        /// <param name="fileCount">A reference to the file count variable</param>
        private void RecursiveCopy(string sourcePath, string destPath, bool verbose, ref int fileCount)
        {
            if(sourcePath == null)
                throw new ArgumentNullException("sourcePath");

            if(destPath == null)
                throw new ArgumentNullException("destPath");

            int idx = sourcePath.LastIndexOf('\\');

            string dirName = sourcePath.Substring(0, idx), fileSpec = sourcePath.Substring(idx + 1), filename;

            foreach(string name in Directory.EnumerateFiles(dirName, fileSpec))
            {
                filename = destPath + Path.GetFileName(name);

                if(!Directory.Exists(destPath))
                    Directory.CreateDirectory(destPath);

                // All attributes are turned off so that we can delete it later
                File.Copy(name, filename, true);
                File.SetAttributes(filename, FileAttributes.Normal);

                if(verbose)
                    this.ReportProgress("{0} -> {1}", name, filename);
                else
                {
                    fileCount++;

                    if((fileCount % 100) == 0)
                        this.ReportProgress("Copied {0} files", fileCount);
                }
            }

            // For "*.*", copy subfolders too
            if(fileSpec == "*.*")
            {
                // Ignore hidden folders as they may be under source control and are not wanted
                foreach(string folder in Directory.EnumerateDirectories(dirName))
                    if((File.GetAttributes(folder) & FileAttributes.Hidden) != FileAttributes.Hidden)
                        this.RecursiveCopy(folder + @"\*.*", destPath + folder.Substring(dirName.Length + 1) +
                            @"\", verbose, ref fileCount);
            }
        }
        #endregion

        #region Other stuff
        //=====================================================================

        /// <summary>
        /// This returns a complete list of files for inclusion in the compiled help file
        /// </summary>
        /// <param name="folder">The folder to expand</param>
        /// <param name="format">The HTML help file format</param>
        /// <returns>The full list of all files for the help project</returns>
        /// <remarks>The help file list is expanded to ensure that we get all additional content including all
        /// nested subfolders.  The <b>format</b> parameter determines the format of the returned file list.  For
        /// HTML Help 1, it returns a list of the filenames.  For MS Help 2, it returns the list formatted with
        /// the necessary XML markup.</remarks>
        private string HelpProjectFileList(string folder, HelpFileFormats format)
        {
            StringBuilder sb = new StringBuilder(10240);
            string itemFormat, filename, checkName, sourceFolder = folder;
            bool encode;

            if(folder == null)
                throw new ArgumentNullException("folder");

            if(folder.Length != 0 && folder[folder.Length - 1] != '\\')
                folder += @"\";

            if((format & HelpFileFormats.HtmlHelp1) != 0)
            {
                if(folder.IndexOf(',') != -1 || folder.IndexOf(".h", StringComparison.OrdinalIgnoreCase) != -1)
                    this.ReportWarning("BE0060", "The file path '{0}' contains a comma or '.h' which may " +
                        "cause the Help 1 compiler to fail.", folder);

                if(this.ResolvedHtmlHelpName.IndexOf(',') != -1 ||
                  this.ResolvedHtmlHelpName.IndexOf(".h", StringComparison.OrdinalIgnoreCase) != -1)
                    this.ReportWarning("BE0060", "The HtmlHelpName property value '{0}' contains a comma " +
                        "or '.h' which may cause the Help 1 compiler to fail.", this.ResolvedHtmlHelpName);

                itemFormat = "{0}\r\n";
                encode = false;
            }
            else
            {
                itemFormat = "	<File Url=\"{0}\" />\r\n";
                encode = true;
            }

            foreach(string name in Directory.EnumerateFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
                if(!encode)
                {
                    filename = checkName = name.Replace(folder, String.Empty);

                    if(checkName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ||
                      checkName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        checkName = checkName.Substring(0, checkName.LastIndexOf(".htm",
                            StringComparison.OrdinalIgnoreCase));

                    if(checkName.IndexOf(',') != -1 || checkName.IndexOf(".h",
                      StringComparison.OrdinalIgnoreCase) != -1)
                        this.ReportWarning("BE0060", "The filename '{0}' " +
                            "contains a comma or '.h' which may cause the " +
                            "Help 1 compiler to fail.", filename);

                    sb.AppendFormat(itemFormat, filename);
                }
                else
                    sb.AppendFormat(itemFormat, HttpUtility.HtmlEncode(name.Replace(folder, String.Empty)));

            return sb.ToString();
        }

        /// <summary>
        /// This is used to generate the website helper files and copy the output to the project output folder
        /// ready for use as a website.
        /// </summary>
        private void GenerateWebsite()
        {
            string webWorkingFolder = String.Format(CultureInfo.InvariantCulture, "{0}Output\\{1}",
                workingFolder, HelpFileFormats.Website);
            int fileCount = 0;

            // Generate the full-text index for the ASP.NET search option
            this.ReportProgress(BuildStep.GenerateFullTextIndex, "Generating full-text index for the website...\r\n");

            if(!this.ExecutePlugIns(ExecutionBehaviors.InsteadOf))
            {
                this.ExecutePlugIns(ExecutionBehaviors.Before);

                FullTextIndex index = new FullTextIndex(workingFolder + "StopWordList.txt", language);
                index.CreateFullTextIndex(webWorkingFolder);
                index.SaveIndex(webWorkingFolder + @"\fti\");

                this.ExecutePlugIns(ExecutionBehaviors.After);
            }

            this.ReportProgress(BuildStep.CopyingWebsiteFiles, "Copying website files to output folder...\r\n");

            if(this.ExecutePlugIns(ExecutionBehaviors.InsteadOf))
                return;

            this.ExecutePlugIns(ExecutionBehaviors.Before);

            // Copy the TOC, keyword index, index pages, and tree view stuff
            File.Copy(workingFolder + "WebTOC.xml", outputFolder + "WebTOC.xml");
            File.Copy(workingFolder + "WebKI.xml", outputFolder + "WebKI.xml");

            // Copy the help pages and related content
            this.RecursiveCopy(webWorkingFolder + @"\*.*", outputFolder, false, ref fileCount);
            this.ReportProgress("Copied {0} files for the website content", fileCount);

            this.GatherBuildOutputFilenames();
            this.ExecutePlugIns(ExecutionBehaviors.After);
        }

        /// <summary>
        /// This is called to generate the HTML table of contents when creating the website output
        /// </summary>
        /// <returns>The HTML to insert for the table of contents</returns>
        private string GenerateHtmlToc()
        {
            XPathDocument tocDoc;
            XPathNavigator navToc;
            XPathNodeIterator entries;
            Encoding enc = Encoding.Default;
            StringBuilder sb = new StringBuilder(2048);

            string content;

            // When reading the file, use the default encoding but detect the
            // encoding if byte order marks are present.
            content = BuildProcess.ReadWithEncoding(workingFolder + "WebTOC.xml", ref enc);

            using(StringReader sr = new StringReader(content))
            {
                tocDoc = new XPathDocument(sr);
            }

            navToc = tocDoc.CreateNavigator();

            // Get the TOC entries from the HelpTOC node
            entries = navToc.Select("HelpTOC/*");

            this.AppendTocEntry(entries, sb);

            return sb.ToString();
        }

        /// <summary>
        /// This is called to recursively append the child nodes to the HTML table of contents in the specified
        /// string builder.
        /// </summary>
        /// <param name="entries">The list over which to iterate recursively.</param>
        /// <param name="sb">The string builder to which the entries are appended.</param>
        private void AppendTocEntry(XPathNodeIterator entries, StringBuilder sb)
        {
            string url, target, title;

            foreach(XPathNavigator node in entries)
                if(node.HasChildren)
                {
                    url = node.GetAttribute("Url", String.Empty);
                    title = node.GetAttribute("Title", String.Empty);

                    if(!String.IsNullOrEmpty(url))
                        target = " target=\"TopicContent\"";
                    else
                    {
                        url = "#";
                        target = String.Empty;
                    }

                    sb.AppendFormat("<div class=\"TreeNode\">\r\n" +
                        "<img class=\"TreeNodeImg\" " +
                        "onclick=\"javascript: Toggle(this);\" " +
                        "src=\"Collapsed.gif\"/><a class=\"UnselectedNode\" " +
                        "onclick=\"javascript: return Expand(this);\" " +
                        "href=\"{0}\"{1}>{2}</a>\r\n" +
                        "<div class=\"Hidden\">\r\n", HttpUtility.HtmlEncode(url), target,
                        HttpUtility.HtmlEncode(title));

                    // Append child nodes
                    this.AppendTocEntry(node.Select("*"), sb);

                    // Write out the closing tags for the root node
                    sb.Append("</div>\r\n</div>\r\n");
                }
                else
                {
                    title = node.GetAttribute("Title", String.Empty);
                    url = node.GetAttribute("Url", String.Empty);

                    if(String.IsNullOrEmpty(url))
                        url = "about:blank";

                    // Write out a TOC entry
                    sb.AppendFormat("<div class=\"TreeItem\">\r\n" +
                        "<img src=\"Item.gif\"/>" +
                        "<a class=\"UnselectedNode\" " +
                        "onclick=\"javascript: return SelectNode(this);\" " +
                        "href=\"{0}\" target=\"TopicContent\">{1}</a>\r\n" +
                        "</div>\r\n", HttpUtility.HtmlEncode(url), HttpUtility.HtmlEncode(title));
                }
        }

        /// <summary>
        /// This is used to ensure that all output folders exist based on the selected help file format(s)
        /// </summary>
        /// <param name="subFolder">The subfolder name or null to ensure that the base folders exist.</param>
        /// <remarks>This creates the named folder under the help format specific folder beneath the
        /// <b>.\Output</b> folder.</remarks>
        public void EnsureOutputFoldersExist(string subFolder)
        {
            if(this.HelpFormatOutputFolders.Count == 0)
            {
                foreach(HelpFileFormats value in Enum.GetValues(typeof(HelpFileFormats)))
                    if((project.HelpFileFormat & value) != 0)
                        this.HelpFormatOutputFolders.Add(String.Format(CultureInfo.InvariantCulture,
                            @"{0}Output\{1}\", workingFolder, value));
            }

            if(!String.IsNullOrEmpty(subFolder))
                foreach(string baseFolder in this.HelpFormatOutputFolders)
                    if(!Directory.Exists(baseFolder + subFolder))
                        Directory.CreateDirectory(baseFolder + subFolder);
        }
        #endregion
    }
}
