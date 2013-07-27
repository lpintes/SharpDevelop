using System;
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
			this.rangeStart=CreateAnchor(startOffset, true);
			this.rangeEnd=CreateAnchor(endOffset, false);
			Debug.WriteLine("TextRange created.");
		}
		
		internal TextRange(TextEditor textEditor, TextAnchor rangeStart, TextAnchor rangeEnd) {
			this.textEditor=textEditor;
			this.rangeStart=rangeStart;
			this.rangeEnd=rangeEnd;
			Debug.WriteLine("TextRange created.");
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
		
		internal TextAnchor CreateAnchor(int offset, bool startOfRange) {
			TextAnchor result=Document.CreateAnchor(offset);
			if(startOfRange) result.MovementType=AnchorMovementType.AfterInsertion;
			else result.MovementType=AnchorMovementType.BeforeInsertion;
			Debug.WriteLine("Anchor created at offset "+offset);
			return result;
		}
		
		internal void GetWordOffsets(ITextSource source, int offset, out int startOffset, out int endOffset) {
			startOffset=endOffset=-1;
			int textLength=source.TextLength;
			if(offset<0 || offset>=textLength) return;
			char ch=source.GetCharAt(offset);
			CharacterClass c=TextUtilities.GetCharacterClass(ch);
			if(c==CharacterClass.Other) {
				startOffset=offset;
				if(offset<textLength) endOffset=offset+1;
				return;
			}
			
		}
		
		internal TextAnchor FindUnitBoundary(TextUnit unit, int offset, LogicalDirection direction) {
			CaretPositioningMode mode=CaretPositioningMode.Normal;
			int rangeOffset=rangeStart.Offset;
			bool startOfRange=(direction==LogicalDirection.Backward);
			int textLength=Document.TextLength;
			switch(unit) {
				case TextUnit.Character:
				case TextUnit.Format:
					if(startOfRange) return CreateAnchor(rangeOffset, true);
					if(rangeOffset<textLength) rangeOffset++;
					return CreateAnchor(rangeOffset,false);
				case TextUnit.Word:
					CharacterClass c=TextUtilities.GetCharacterClass(Document.GetCharAt(rangeOffset));
					if(c==CharacterClass.Other) {
						if(startOfRange) return CreateAnchor(rangeOffset, true);
						rangeOffset+=1;
						if(rangeOffset>=textLength) rangeOffset=textLength;
						return CreateAnchor(rangeOffset, false);
					}
					else {
						if(startOfRange) mode=CaretPositioningMode.WordStartOrSymbol;
						else mode=CaretPositioningMode.WordBorderOrSymbol;
						int temp=TextUtilities.GetNextCaretPosition(Document, rangeOffset, direction, mode);
						if(temp!=-1) rangeOffset=temp;
						return CreateAnchor(rangeOffset, startOfRange);
					}
				case TextUnit.Line:
					VisualLine line=TextView.GetOrConstructVisualLine(Document.GetLineByOffset(offset));
					rangeOffset=line.StartOffset;
					if(!startOfRange) rangeOffset+=line.VisualLengthWithEndOfLineMarker+1;
					return CreateAnchor(rangeOffset, startOfRange);
				case TextUnit.Paragraph:
					DocumentLine dline=Document.GetLineByOffset(offset);
					if(startOfRange) rangeOffset=dline.Offset;
					else rangeOffset=dline.EndOffset+dline.DelimiterLength;
					return CreateAnchor(rangeOffset, startOfRange);
				case TextUnit.Page:
				case TextUnit.Document:
					rangeOffset=(startOfRange ? 0 : Document.TextLength);
					return CreateAnchor(rangeOffset, startOfRange);
			}
			return rangeStart;
		}
		
		internal int MoveBoundaryByUnit(ref TextAnchor boundary, TextUnit unit, int count) {
			if(count==0) return 0;
			LogicalDirection direction=(count<0 ? LogicalDirection.Backward : LogicalDirection.Forward);
			int textLength=Document.TextLength;
			int result=0;
			int delta=0;
			if(unit==TextUnit.Line) delta=1;
			if(direction==LogicalDirection.Backward) delta=-delta;
			boundary=FindUnitBoundary(unit, boundary.Offset, LogicalDirection.Backward);
			count=Math.Abs(count);
			for(int i=0; i<count; i++) {
				boundary=FindUnitBoundary(unit, boundary.Offset+delta, direction);
				int offset=boundary.Offset;
				if(offset<=0 || offset>=textLength) break;
				result++;
			}
			return result;
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
			int offset=rangeStart.Offset;
			rangeStart=FindUnitBoundary(unit, offset, LogicalDirection.Backward);
			rangeEnd=FindUnitBoundary(unit, offset, LogicalDirection.Forward);
		}
		
		int ITextRangeProvider.Move(TextUnit unit, int count) {
			rangeStart=FindUnitBoundary(unit, rangeStart.Offset, LogicalDirection.Backward);
			int result=MoveBoundaryByUnit(ref rangeStart, unit, count);
			rangeEnd=FindUnitBoundary(unit, rangeStart.Offset, LogicalDirection.Forward);
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
			TextAnchor boundary=(endpoint==TextPatternRangeEndpoint.Start ? rangeStart : rangeEnd);
			int result=MoveBoundaryByUnit(ref boundary, unit, count);
			if(rangeStart.Offset>rangeEnd.Offset) {
				TextAnchor a=rangeStart;
				rangeStart=rangeEnd;
				rangeEnd=a;
			}
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
