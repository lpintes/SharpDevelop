﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.SharpDevelop.Gui;
using System;
using System.Xml;

namespace ICSharpCode.WixBinding
{
	public class WixComponentTreeNode : WixTreeNode
	{		
		public WixComponentTreeNode(WixComponentElement element) : base(element)
		{
			SetIcon("Icons.16x16.SolutionIcon");
			ContextmenuAddinTreePath = "/AddIns/WixBinding/PackageFilesView/ContextMenu/ComponentTreeNode";
			Refresh();
		}
		
		public override void Refresh()
		{
			base.Refresh();
			Text = XmlElement.GetAttribute("Id");
		}
	}
}
