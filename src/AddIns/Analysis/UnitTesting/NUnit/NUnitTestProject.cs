﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.NRefactory.Utils;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.UnitTesting
{
	/// <summary>
	/// NUnit test project.
	/// </summary>
	public class NUnitTestProject : TestProjectBase
	{
		public NUnitTestProject(IProject project) : base(project)
		{
		}
		
		public override ITestRunner CreateTestRunner(TestExecutionOptions options)
		{
			if (options.UseDebugger)
				return new NUnitTestDebugger();
			return new NUnitTestRunner(options);
		}
		
		protected override bool IsTestClass(ITypeDefinition typeDefinition)
		{
			return NUnitTestFramework.IsTestClass(typeDefinition);
		}
		
		protected override ITest CreateTestClass(ITypeDefinition typeDefinition)
		{
			if (NUnitTestFramework.IsTestClass(typeDefinition))
				return new NUnitTestClass(this, typeDefinition.FullTypeName);
			else
				return null;
		}
		
		protected override void UpdateTestClass(ITest test, ITypeDefinition typeDefinition)
		{
			((NUnitTestClass)test).UpdateTestClass(typeDefinition);
		}
		
		protected override void OnTestClassRemoved(ITest test)
		{
			((NUnitTestClass)test).OnRemoved();
			base.OnTestClassRemoved(test);
		}
		
		public override IEnumerable<ITest> GetTestsForEntity(IEntity entity)
		{
			ITypeDefinition typeDefinition = entity as ITypeDefinition ?? entity.DeclaringTypeDefinition;
			IEnumerable<ITest> tests;
			if (typeDefinition.IsAbstract) {
				tests = from c in entity.ParentAssembly.GetAllTypeDefinitions()
					where c.IsDerivedFrom(typeDefinition)
					select GetTestForEntityInClass(GetTestClass(c.FullTypeName), entity);
			} else {
				NUnitTestClass c = GetTestClass(typeDefinition.FullTypeName);
				tests = new [] { GetTestForEntityInClass(c, entity) };
			}
			// GetTestForEntityInClass might return null, so filter those out:
			return tests.Where(t => t != null);
		}
		
		ITest GetTestForEntityInClass(NUnitTestClass c, IEntity entity)
		{
			if (c == null)
				return null;
			if (entity.SymbolKind == SymbolKind.TypeDefinition)
				return c;
			else if (entity.SymbolKind == SymbolKind.Method)
				return c.FindTestMethod(entity.Name);
			else
				return null;
		}
		
		public override void UpdateTestResult(TestResult result)
		{
			int lastDot = result.Name.LastIndexOf('.');
			if (lastDot < 0)
				return;
			string fixtureName = result.Name.Substring(0, lastDot);
			string methodName = result.Name.Substring(lastDot + 1);
			NUnitTestClass testClass = GetTestClass(new FullTypeName(fixtureName));
			if (testClass == null) {
				// maybe it's an inherited test
				int secondToLastDot = result.Name.LastIndexOf('.', lastDot - 1);
				if (secondToLastDot >= 0) {
					string fixtureName2 = result.Name.Substring(0, secondToLastDot);
					methodName = result.Name.Substring(secondToLastDot + 1);
					testClass = GetTestClass(new FullTypeName(fixtureName2));
				}
			}
			if (testClass != null) {
				NUnitTestMethod testMethod = testClass.FindTestMethod(methodName);
				if (testMethod != null) {
					testMethod.UpdateTestResult(result);
				}
			}
		}
		
		public NUnitTestClass GetTestClass(FullTypeName fullTypeName)
		{
			var testClass = (NUnitTestClass)base.GetTestClass(fullTypeName.TopLevelTypeName);
			int tpc = fullTypeName.TopLevelTypeName.TypeParameterCount;
			for (int i = 0; i < fullTypeName.NestingLevel; i++) {
				if (testClass == null)
					break;
				tpc += fullTypeName.GetNestedTypeAdditionalTypeParameterCount(i);
				testClass = testClass.FindNestedTestClass(fullTypeName.GetNestedTypeName(i), tpc);
			}
			return testClass;
		}
		
		#region Test Inheritance
		MultiDictionary<TopLevelTypeName, NUnitTestClass> inheritedTestClasses = new MultiDictionary<TopLevelTypeName, NUnitTestClass>();
		
		public void RegisterInheritedClass(FullTypeName baseClassName, NUnitTestClass inheritedClass)
		{
			inheritedTestClasses.Add(baseClassName.TopLevelTypeName, inheritedClass);
		}
		
		public void RemoveInheritedClass(FullTypeName baseClassName, NUnitTestClass inheritedClass)
		{
			inheritedTestClasses.Remove(baseClassName.TopLevelTypeName, inheritedClass);
		}
		
		protected override void AddToDirtyList(TopLevelTypeName className)
		{
			// When a base class is invalidated, also invalidate all derived classes
			base.AddToDirtyList(className);
			foreach (var derivedClass in inheritedTestClasses[className]) {
				base.AddToDirtyList(derivedClass.FullTypeName.TopLevelTypeName);
			}
		}
		#endregion
		
	}
}
