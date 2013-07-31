using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Documents;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;

namespace ICSharpCode.AvalonEdit {
	internal class TextRange : ITextRangeProvider {
		internal TextAnchor rangeStart, rangeEnd;
		internal TextEditor textEditor;
		
		internal TextRange(TextEditor textEditor, int startOffset, int endOffset) {
			if(textEditor==null) throw new ArgumentNullException("textEditor");
			if(startOffset<0) throw new ArgumentException("Negative startOffset");
			if(endOffset<0) throw new ArgumentException("Negative endOffset");
			if(startOffset>endOffset) {
				int x=startOffset;
				startOffset=endOffset;
				endOffset=x;
			}
			this.textEditor=textEditor;
			CreateAnchors(startOffset, endOffset);
			Debug.WriteLine("TextRange created from offsets.");
		}
		
		internal TextRange(TextEditor textEditor, TextAnchor rangeStart, TextAnchor rangeEnd) {
			this.textEditor=textEditor;
			this.rangeStart=rangeStart;
			this.rangeEnd=rangeEnd;
			Debug.WriteLine("TextRange created from anchors.");
		}
		
		internal TextDocument Document {
			get {
				TextDocument doc=textEditor.Document;
				if(doc==null) throw new InvalidOperationException("No document associated with this editor");
				return doc;
			}
		}
		
		internal TextView TextView {
			get { return textEditor.TextArea.TextView; }
		}
		
		internal void CreateAnchors(int startOffset, int endOffset) {
			rangeStart=Document.CreateAnchor(startOffset);
			rangeStart.MovementType=AnchorMovementType.AfterInsertion;
			rangeEnd=Document.CreateAnchor(endOffset);
			rangeEnd.MovementType=AnchorMovementType.BeforeInsertion;
			Debug.WriteLine("Range created at offsets "+startOffset+", "+ endOffset);
		}
		
		internal void GetUnitOffsets(TextUnit unit, ITextSource source, int offset, out int startOffset, out int endOffset) {
			int textLength=source.TextLength;
			startOffset=endOffset=-1;
			if(offset<0 || offset>=textLength-1) return;
			switch(unit) {
				case TextUnit.Character:
				case TextUnit.Format:
					startOffset=offset;
					endOffset=offset+1;
				break;
				case TextUnit.Word:
					if(char.IsWhiteSpace(source.GetCharAt(offset))) {
						startOffset=offset;
						endOffset=offset+1;
					}
					else {
						endOffset=TextUtilities.GetNextCaretPosition(source, offset, LogicalDirection.Forward, CaretPositioningMode.WordStartOrSymbol);
						startOffset=TextUtilities.GetNextCaretPosition(source, endOffset, LogicalDirection.Backward, CaretPositioningMode.WordStartOrSymbol);
					}
				break;
				case TextUnit.Line:
					VisualLine line=TextView.GetOrConstructVisualLine(Document.GetLineByOffset(offset));
					startOffset=line.StartOffset;
					endOffset=startOffset+line.VisualLengthWithEndOfLineMarker+1;
				break;
				case TextUnit.Paragraph:
					DocumentLine dline=Document.GetLineByOffset(offset);
					startOffset=dline.Offset;
					endOffset=dline.EndOffset+dline.DelimiterLength;
				break;
				case TextUnit.Page:
				case TextUnit.Document:
					startOffset=0;
					endOffset=textLength;
				break;
			}
			if(startOffset==-1) startOffset=0;
			if(endOffset==-1) endOffset=textLength;
		}
		
		
		ITextRangeProvider ITextRangeProvider.Clone() {
			return (ITextRangeProvider)new TextRange(textEditor, rangeStart, rangeEnd);
		}
		
		bool ITextRangeProvider.Compare(ITextRangeProvider range) {
			if(!(range is TextRange)) throw new ArgumentException("Unsupported text provider");
			TextRange r =range as TextRange;
			return(rangeStart.Offset==r.rangeStart.Offset && rangeEnd.Offset==r.rangeEnd.Offset);
		}
		
		int ITextRangeProvider.CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint) {
			if(!(targetRange is TextRange)) throw new ArgumentException("Unsupported text provider");
			TextRange r=targetRange as TextRange;
			TextAnchor ep1, ep2;
			ep1=(endpoint==TextPatternRangeEndpoint.Start ? rangeStart : rangeEnd);
			ep2=(targetEndpoint==TextPatternRangeEndpoint.Start ? r.rangeStart : r.rangeEnd);
			return ep1.Offset-ep2.Offset;
		}
		
		void ITextRangeProvider.ExpandToEnclosingUnit(TextUnit unit) {
			int startOffset=-1, endOffset;
			GetUnitOffsets(unit, Document, rangeStart.Offset, out startOffset, out endOffset);
			CreateAnchors(startOffset, endOffset);
		}
		
		internal int MoveOffsetByUnit(TextUnit unit, int count, ref int offset) {
			int startOffset, endOffset;
			int result=0;
			int textLength=Document.TextLength;
			GetUnitOffsets(unit, Document, offset, out startOffset, out endOffset);
			for(int i=0; i<Math.Abs(count); i++) {
				if(count<0) offset=startOffset-1;
				else offset=endOffset;
				GetUnitOffsets(unit, Document, offset, out startOffset, out endOffset);
				if(startOffset==0 || endOffset==textLength) break;
				result++;
			}
			return result;
		}
		
		int ITextRangeProvider.Move(TextUnit unit, int count) {
			int startOffset=rangeStart.Offset, endOffset=-1;
			int result=MoveOffsetByUnit(unit, count, ref startOffset);
			GetUnitOffsets(unit, Document, startOffset, out startOffset, out endOffset);
			CreateAnchors(startOffset, endOffset);
			return result;
		}
		
		void ITextRangeProvider.MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint) {
			if(!(targetRange is TextRange)) throw new ArgumentException("Unsupported text provider");
			TextRange r=targetRange as TextRange;
			TextAnchor ep;
			ep=(targetEndpoint==TextPatternRangeEndpoint.Start ? r.rangeStart : r.rangeEnd);
			if(endpoint==TextPatternRangeEndpoint.Start) rangeStart=ep;
			else rangeEnd=ep;
			if(rangeStart.Offset>rangeEnd.Offset) {
				TextAnchor a=rangeStart;
				rangeStart=rangeEnd;
				rangeEnd=a;
			}
		}
		
		int ITextRangeProvider.MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count) {
			TextAnchor ep1, ep2;
			ep1=(endpoint==TextPatternRangeEndpoint.Start ? rangeStart : rangeEnd);
			ep2=(ep1==rangeStart ? rangeEnd : rangeStart);
			int startOffset=ep1.Offset, endOffset;
			int result=MoveOffsetByUnit(unit, count, ref startOffset);
			if(startOffset<ep2.Offset) endOffset=ep2.Offset;
			else {
				endOffset=startOffset;
				startOffset=ep2.Offset;
			}
			CreateAnchors(startOffset, endOffset);
			return result;
		}
		
		void ITextRangeProvider.AddToSelection() {
			throw new InvalidOperationException("Multiple selections are not supported");
		}
		
		void ITextRangeProvider.RemoveFromSelection() {
			throw new InvalidOperationException("Multiple selections are not supported");
		}
		
		void ITextRangeProvider.Select() {
			Selection.Create(textEditor.TextArea, rangeStart.Offset, rangeEnd.Offset);
		}
		
		ITextRangeProvider ITextRangeProvider.FindAttribute(int attribute, object value, bool backward) {
			return null;
		}
		
		ITextRangeProvider ITextRangeProvider.FindText(string text, bool backward, bool ignoreCase) {
			ISearchStrategy search=SearchStrategyFactory.Create(text, ignoreCase, false, SearchMode.Normal);
			int offset=rangeStart.Offset;
			int length=rangeEnd.Offset-offset;
			ISearchResult result=search.FindAll(Document, offset, length).FirstOrDefault();
			if(result==null) return null;
			return (ITextRangeProvider)new TextRange(textEditor, result.Offset, result.EndOffset);
		}
		
		object ITextRangeProvider.GetAttributeValue(int attribute) {
			return AutomationElementIdentifiers.NotSupported;
		}
		
		string ITextRangeProvider.GetText(int maxLength) {
			if(maxLength<-1) throw new ArgumentOutOfRangeException("maxLength");
			int offset=rangeStart.Offset;
			int length=rangeEnd.Offset-offset;
			if(maxLength==-1) maxLength=length;
			else if(maxLength>0) maxLength=Math.Max(length, maxLength);
			else maxLength=0;
			return Document.GetText(offset, maxLength);
		}
		
		IRawElementProviderSimple ITextRangeProvider.GetEnclosingElement() {
			//!
			return (IRawElementProviderSimple)textEditor;
		}
		
		IRawElementProviderSimple[] ITextRangeProvider.GetChildren() {
			//!
			return new IRawElementProviderSimple[0];
		}
		
		double[] ITextRangeProvider.GetBoundingRectangles() {
			//!
			return new double[0];
		}
		
		void ITextRangeProvider.ScrollIntoView(bool alignToTop) {
			textEditor.ScrollToLine(Document.GetLineByOffset(rangeStart.Offset).LineNumber);
		}
		
		
	}
}
