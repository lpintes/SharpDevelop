﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using ICSharpCode.Core;
using ICSharpCode.UnitTesting;
using NUnit.Framework;
using UnitTesting.Tests.Utils;

namespace UnitTesting.Tests.Frameworks
{
	[TestFixture]
	public class TestFrameworkDescriptorSupportsCSharpProjectsTestFixture
	{
		TestFrameworkDescriptor descriptor;
		MockTestFramework fakeTestFramework;
		
		[SetUp]
		public void Init()
		{
			MockTestFrameworkFactory factory = new MockTestFrameworkFactory();
			fakeTestFramework = new MockTestFramework();
			factory.Add("NUnitTestFramework", fakeTestFramework);
			
			Properties properties = new Properties();
			properties["id"] = "nunit";
			properties["supportedProjects"] = ".csproj";
			properties["class"] = "NUnitTestFramework";
			
			descriptor = new TestFrameworkDescriptor(properties, factory);
		}
		
		[Test]
		public void IsSupportedProjectReturnsTrueForCSharpProject()
		{
			MockCSharpProject project = new MockCSharpProject();
			fakeTestFramework.AddTestProject(project);
			project.FileName = @"d:\projects\myproj.csproj";
			
			Assert.IsTrue(descriptor.IsSupportedProject(project));
		}
		
		[Test]
		public void IsSupportedProjectReturnsFalseForVBNetProject()
		{
			MockCSharpProject project = new MockCSharpProject();
			fakeTestFramework.AddTestProject(project);
			project.FileName = @"d:\projects\myproj.vbproj";
			
			Assert.IsFalse(descriptor.IsSupportedProject(project));
		}
		
		[Test]
		public void IsSupportedProjectReturnsFalseForNullProject()
		{
			Assert.IsFalse(descriptor.IsSupportedProject(null));
		}
		
		[Test]
		public void IsSupportedProjectReturnsTrueForCSharpProjectFileExtensionInUpperCase()
		{
			MockCSharpProject project = new MockCSharpProject();
			fakeTestFramework.AddTestProject(project);
			project.FileName = @"d:\projects\myproj.CSPROJ";
			
			Assert.IsTrue(descriptor.IsSupportedProject(project));
		}
	}
}
