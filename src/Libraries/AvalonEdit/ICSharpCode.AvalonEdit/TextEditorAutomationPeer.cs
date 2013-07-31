// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace ICSharpCode.AvalonEdit
{
	/// <summary>
	/// Exposes <see cref="TextEditor"/> to automation.
	/// </summary>
	public class TextEditorAutomationPeer : FrameworkElementAutomationPeer, IValueProvider, ITextProvider
	{
		/// <summary>
		/// Creates a new TextEditorAutomationPeer instance.
		/// </summary>
		public TextEditorAutomationPeer(TextArea owner) : base(owner)
		{
			Debug.WriteLine("TextEditorAutomationPeer was created");
		}
		
		private TextEditor TextEditor {
			get { return ((TextArea)base.Owner).TextEditor; }
		}
		
		void IValueProvider.SetValue(string value)
		{
			this.TextEditor.Text = value;
		}
		
		string IValueProvider.Value {
			get { return this.TextEditor.Text; }
		}
		
		bool IValueProvider.IsReadOnly {
			get { return this.TextEditor.IsReadOnly; }
		}
		
		ITextRangeProvider ITextProvider.DocumentRange {
			get {
				TextDocument document=TextEditor.Document;
				return (ITextRangeProvider)new TextRange(TextEditor,0,document.TextLength);
			}
		}
		
		SupportedTextSelection ITextProvider.SupportedTextSelection {
			get { return SupportedTextSelection.Single; }
		}
		
		ITextRangeProvider[] ITextProvider.GetSelection() {
			int startOffset=TextEditor.SelectionStart;
			int endOffset=startOffset+TextEditor.SelectionLength;
			var result=new ITextRangeProvider[1];
			result[0]=(ITextRangeProvider)new TextRange(TextEditor, startOffset, endOffset);
			return result;
		}
		
		ITextRangeProvider[] ITextProvider.GetVisibleRanges() {
			var result=new List<ITextRangeProvider>();
			var textView=TextEditor.TextArea.TextView;
			textView.EnsureVisualLines();
			foreach(var vline in textView.VisualLines) {
				int startOffset=vline.StartOffset;
				int endOffset=startOffset+vline.VisualLength;
				result.Add((ITextRangeProvider)new TextRange(TextEditor, startOffset, endOffset));
			}
			return result.ToArray();
		}
		
		ITextRangeProvider ITextProvider.RangeFromChild(IRawElementProviderSimple  childElement) {
			//!
			if(childElement==null) throw new ArgumentNullException("childElement");
			return (ITextRangeProvider)new TextRange(TextEditor, 0, 0);
		}
		
		ITextRangeProvider ITextProvider.RangeFromPoint(Point screenLocation) {
			//!
			screenLocation=screenLocation.TransformFromDevice(TextEditor);
			TextViewPosition? pos=TextEditor.GetPositionFromPoint(screenLocation);
			if(pos==null) throw new ArgumentException("screenLocation");
			VisualLine line=TextEditor.TextArea.TextView.GetVisualLine(pos.Value.Line);
			int startOffset=line.StartOffset;
			int endOffset=startOffset+line.VisualLength;
			return (ITextRangeProvider) new TextRange(TextEditor, startOffset, endOffset);
		}
		/// <inheritdoc/>
		protected override AutomationControlType GetAutomationControlTypeCore() {
			return AutomationControlType.Document;
		}
		
		/// <inheritdoc/>
		protected override string GetClassNameCore() {
			return "AvalonEdit";
		}
		
		/// <inheritdoc/>
		protected override bool HasKeyboardFocusCore() {
			return TextEditor.TextArea.IsKeyboardFocusWithin;
		}
		
		/// <inheritdoc/>
		protected override bool IsKeyboardFocusableCore() {
			return true;
		}
		
		/// <inheritdoc/>
		public override object GetPattern(PatternInterface patternInterface)
		{
			if (patternInterface == PatternInterface.Value || patternInterface==PatternInterface.Text)
				return this;
			
			if (patternInterface == PatternInterface.Scroll) {
				ScrollViewer scrollViewer = this.TextEditor.ScrollViewer;
				if (scrollViewer != null)
					return UIElementAutomationPeer.CreatePeerForElement(scrollViewer);
			}
			
			return base.GetPattern(patternInterface);
		}
		
		internal void RaiseIsReadOnlyChanged(bool oldValue, bool newValue)
		{
			RaisePropertyChangedEvent(ValuePatternIdentifiers.IsReadOnlyProperty, Boxes.Box(oldValue), Boxes.Box(newValue));
		}
	}
}
