// Copyright (c) 2026 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Threading.Tasks;

using Avalonia.Headless.NUnit;

using AwesomeAssertions;

using ICSharpCode.ILSpyX;

using ICSharpCode.ILSpy.AppEnv;

using NUnit.Framework;

namespace ICSharpCode.ILSpy.Tests;

/// <summary>
/// The target-framework override lets the user hint the TFM used to resolve an assembly's
/// references instead of the one detected from its TargetFrameworkAttribute. These tests pin
/// the load-bearing behaviour: the override wins over detection, survives the assembly-list
/// XML round-trip, defaults to null for assemblies persisted without it, and is carried across
/// a reload (which is how a runtime change re-resolves against the new framework).
/// </summary>
[TestFixture]
public class TargetFrameworkOverrideTests
{
	static AssemblyListManager Manager => AppComposition.Current.GetExport<SettingsService>().AssemblyListManager;

	[AvaloniaTest]
	public async Task Override_Wins_Over_Detected_Tfm_Even_After_Detection_Cached()
	{
		var list = Manager.CreateList("tfm-override-" + Guid.NewGuid().ToString("N"));
		var asm = list.OpenAssembly(FixtureAssembly.Emit("TfmOverrideFixture"));

		// Force detection to run and cache its result first, so the test also proves the override
		// takes precedence over an already-cached detected value.
		var detected = await asm.GetTargetFrameworkIdAsync();
		asm.TargetFrameworkIdOverride = ".NETFramework,Version=v4.7.2";

		(await asm.GetTargetFrameworkIdAsync()).Should().Be(".NETFramework,Version=v4.7.2");
		detected.Should().NotBe(".NETFramework,Version=v4.7.2",
			"the fixture is not a .NET Framework 4.7.2 assembly; the override is what changes the result");
	}

	[AvaloniaTest]
	public void Override_Survives_SaveAsXml_Roundtrip()
	{
		var list = Manager.CreateList("tfm-roundtrip-" + Guid.NewGuid().ToString("N"));
		var asm = list.OpenAssembly(FixtureAssembly.Emit("TfmRoundtripFixture"));
		asm.TargetFrameworkIdOverride = ".NETCoreApp,Version=v6.0";

		var reloaded = new AssemblyList(Manager, list.SaveAsXml());

		reloaded.GetAssemblies().Should().ContainSingle()
			.Which.TargetFrameworkIdOverride.Should().Be(".NETCoreApp,Version=v6.0");
	}

	[AvaloniaTest]
	public void Assembly_Without_Override_Persists_No_Attribute_And_Loads_Null()
	{
		var list = Manager.CreateList("tfm-noattr-" + Guid.NewGuid().ToString("N"));
		list.OpenAssembly(FixtureAssembly.Emit("TfmNoAttrFixture"));

		var xml = list.SaveAsXml();
		xml.Elements("Assembly").Should().OnlyContain(e => e.Attribute("TargetFramework") == null,
			"no attribute is written when there is no override (backward-compatible XML)");

		var reloaded = new AssemblyList(Manager, xml);
		reloaded.GetAssemblies().Should().ContainSingle()
			.Which.TargetFrameworkIdOverride.Should().BeNull();
	}

	[AvaloniaTest]
	public void ReloadAssembly_Carries_Override_To_New_Instance()
	{
		var list = Manager.CreateList("tfm-reload-" + Guid.NewGuid().ToString("N"));
		var asm = list.OpenAssembly(FixtureAssembly.Emit("TfmReloadFixture"));
		asm.TargetFrameworkIdOverride = ".NETStandard,Version=v2.0";

		var reloaded = list.ReloadAssembly(asm);

		((object?)reloaded).Should().NotBeNull();
		reloaded!.Should().NotBeSameAs(asm);
		reloaded.TargetFrameworkIdOverride.Should().Be(".NETStandard,Version=v2.0");
	}
}
