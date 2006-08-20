﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.SharpDevelop.DefaultEditor.Gui.Editor;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.TextEditor;
using ICSharpCode.TextEditor.Document;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace ICSharpCode.WixBinding
{
	/// <summary>
	/// Displays the setup package files.
	/// </summary>
	public class PackageFilesView : AbstractViewContent, ITextFileReader, IWixDocumentWriter
	{
		WixPackageFilesControl packageFilesControl;
		WorkbenchTextFileReader textFileReader = new WorkbenchTextFileReader();
		WixProject project;
		bool reload;

		public override Control Control {
			get {
				return packageFilesControl;
			}
		}
		
		PackageFilesView(WixProject project)
		{
			packageFilesControl = new WixPackageFilesControl();
			packageFilesControl.DirtyChanged += PackageFilesControlDirtyChanged;
			TitleName = "Setup Files";
			this.project = project;
			FileName = "Dummy.filename";
			
			WorkbenchSingleton.Workbench.ActiveWorkbenchWindowChanged += ActiveWorkbenchWindowChanged;
		}
		
		public static PackageFilesView ActiveView {
			get {
				return WorkbenchSingleton.Workbench.ActiveContent as PackageFilesView;
			}
		}
		
		/// <summary>
		/// Gets the project that this view is associated with.
		/// </summary>
		public WixProject Project {
			get {
				return project;
			}
			set {
				project = value;
			}
		}
		
		public override void Save()
		{
			packageFilesControl.Save();
		}
		
		public override bool IsDirty {
			get {
				return packageFilesControl.IsDirty;
			}
			set {
				packageFilesControl.IsDirty = value;
			}
		}
		
		/// <summary>
		/// Shows the view for the specified project.
		/// </summary>
		public static void Show(WixProject project, IWorkbench workbench)
		{
			PackageFilesView openView = GetOpenPackageFilesView(project, workbench);
			if (openView != null) {
				openView.WorkbenchWindow.SelectWindow();
			} else {
				PackageFilesView newView = new PackageFilesView(project);
				workbench.ShowView(newView);
				newView.ShowFiles();
			}
		}
		
		public override void Dispose()
		{
			if (packageFilesControl != null) {
				WorkbenchSingleton.Workbench.ActiveWorkbenchWindowChanged -= ActiveWorkbenchWindowChanged;
				packageFilesControl.Dispose();
				packageFilesControl = null;
			}
		}
		
		public TextReader Create(string fileName)
		{
			return textFileReader.Create(fileName);
		}
		
		public void Write(WixDocument document)
		{
			if (!UpdateOpenFile(document)) {
				ITextEditorProperties properties = new SharpDevelopTextEditorProperties();
				document.Save(properties.LineTerminator, properties.ConvertTabsToSpaces, properties.TabIndent);
			}
			IsDirty = false;
		}
		
		/// <summary>
		/// Adds a new child element with the given name to the selected tree node.
		/// </summary>
		public void AddElement(string name)
		{
			packageFilesControl.AddElement(name);
		}
		
		/// <summary>
		/// Removes the selected element from the Wix document.
		/// </summary>
		public void RemoveSelectedElement()
		{
			packageFilesControl.RemoveSelectedElement();
		}
		
		public void AddFiles()
		{
			packageFilesControl.AddFiles();
		}
	
		public void ShowFiles()
		{
			packageFilesControl.ShowFiles(project, this, this);
		}
		
		/// <summary>
		/// Gets the package files view that is already open and displaying the files
		/// for the specified project.
		/// </summary>
		static PackageFilesView GetOpenPackageFilesView(WixProject project, IWorkbench workbench)
		{
			foreach (IViewContent view in workbench.ViewContentCollection) {
				PackageFilesView packageFilesView = view as PackageFilesView;
				if (packageFilesView != null && packageFilesView.Project == project) {
					return packageFilesView;
				}
			}
			return null;
		}
		
		void PackageFilesControlDirtyChanged(object source, EventArgs e)
		{
			base.IsDirty = packageFilesControl.IsDirty;
		}
		
		TextAreaControl GetTextAreaControl(string fileName)
		{
			IWorkbenchWindow openWindow = FileService.GetOpenFile(fileName);
			if (openWindow != null) {
				ITextEditorControlProvider textEditorControlProvider = openWindow.ViewContent as ITextEditorControlProvider;
				if (textEditorControlProvider != null) {
					return textEditorControlProvider.TextEditorControl.ActiveTextAreaControl;
				}
			}
			return null;
		}
		
		/// <summary>
		/// Merges the changes to the Wix document to the file currently open in
		/// SharpDevelop.
		/// </summary>
		bool UpdateOpenFile(WixDocument wixDocument)
		{
			TextAreaControl textAreaControl = GetTextAreaControl(packageFilesControl.Document.FileName);
			if (textAreaControl != null) {
				if (wixDocument.IsProductDocument) {
					UpdateOpenFileWithRootDirectoryChanges(wixDocument, textAreaControl);
				} else {
					// Directory ref.
					UpdateOpenFileWithRootDirectoryRefChanges(wixDocument, textAreaControl);
				}
			}
			return false;
		}
		
		/// <summary>
		/// When the user switches away from the package files view to the corresponding
		/// Wix document then we update the document's contents. When the user switches 
		/// back we reload the view if the corresponding Wix document is open.
		/// </summary>
		void ActiveWorkbenchWindowChanged(object source, EventArgs e)
		{
			if (IsWixDocumentWindowActive) {
				if (IsDirty) {
					// Set IsDirty to false first since we get another workbench window
					// changed event whilst updating the open file. The 
					// DefaultDocument.Replace method triggers this.
					IsDirty = false;
					UpdateOpenFile(packageFilesControl.Document);
				}
				reload = true;
			} else if (reload && IsActiveWindow) {
				ShowFiles();
				reload = false;
			}
		}
		
		/// <summary>
		/// Checks whether the active window is the Wix document window.
		/// </summary>
		bool IsWixDocumentWindowActive {
			get {
				WixDocument document = packageFilesControl.Document;
				if (document != null) {
					IViewContent view = WorkbenchSingleton.Workbench.ActiveContent as IViewContent;
					if (view != null) {
						return FileUtility.IsEqualFileName(view.FileName, document.FileName);
					}
				}
				return false;
			}
		}
			
		/// <summary>
		/// Checks whether the active window is this window.
		/// </summary>
		bool IsActiveWindow {
			get {
				return Object.ReferenceEquals(WorkbenchSingleton.Workbench.ActiveContent, this);
			}
		}	
		
		bool UpdateOpenFileWithRootDirectoryChanges(WixDocument wixDocument, TextAreaControl textAreaControl)
		{
			// Get the xml for the root directory.
			WixDirectoryElement rootDirectory = wixDocument.RootDirectory;
			string xml = GetWixXml(rootDirectory);

			// Find the root directory location.
			bool updated = ReplaceElement(rootDirectory.Id, WixDirectoryElement.DirectoryElementName, textAreaControl, xml);
			if (updated) {
				return true;
			}
			
			// Find the product end element location.
			IDocument document = textAreaControl.Document;
			Location location = WixDocument.GetEndElementLocation(new StringReader(document.TextContent), "Product", wixDocument.Product.GetAttribute("Id"));
			if (!location.IsEmpty) {
				// Insert the xml with an extra new line at the end.
				ITextEditorProperties properties = new SharpDevelopTextEditorProperties();
				WixDocumentEditor documentEditor = new WixDocumentEditor(textAreaControl);
				documentEditor.Insert(location.Y, location.X, String.Concat(xml, properties.LineTerminator));
				return true;
			}
			return false;
		}
		
		bool UpdateOpenFileWithRootDirectoryRefChanges(WixDocument wixDocument, TextAreaControl textAreaControl)
		{
			// Get the xml for the root directory ref.
			WixDirectoryRefElement rootDirectoryRef = wixDocument.RootDirectoryRef;
			string xml = GetWixXml(rootDirectoryRef);

			// Find the root directory ref location.
			return ReplaceElement(rootDirectoryRef.Id, WixDirectoryRefElement.DirectoryRefElementName, textAreaControl, xml);
		}
		
		/// <summary>
		/// Gets the Wix xml for the specified element.
		/// </summary>
		string GetWixXml(XmlElement element)
		{
			ITextEditorProperties properties = new SharpDevelopTextEditorProperties();				
			return WixDocument.GetXml(element, properties.LineTerminator, properties.ConvertTabsToSpaces, properties.TabIndent);
		}
		
		/// <summary>
		/// Tries to replace the element defined by element name and its Id attribute in the
		/// text editor with the specified xml.
		/// </summary>
		/// <param name="id">The Id attribute of the element.</param>
		/// <param name="elementName">The name of the element.</param>
		/// <param name="textAreaControl">The text area control to update.</param>
		/// <param name="xml">The replacement xml.</param>
		bool ReplaceElement(string id, string elementName, TextAreaControl textAreaControl, string xml)
		{
			WixDocumentEditor documentEditor = new WixDocumentEditor(textAreaControl);
			IDocument document = textAreaControl.Document;
			DomRegion region = WixDocument.GetElementRegion(new StringReader(document.TextContent), elementName, id);
			if (!region.IsEmpty) {
				documentEditor.Replace(region, xml);
				return true;
			} 
			return false;
		}
	}
}
