﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building.Model;
using PLang.Utils;

namespace PLangTests.Integration
{
	[TestClass]
	public class ListAndLoopTest : BasePLangTest
	{
		PLang.Modules.FileModule.Program fileProgram;
		PLang.Modules.LoopModule.Program loopProgram;

		[TestInitialize]
		public void Init() {
			base.Initialize();

			fileProgram = new(fileSystem, settings, logger, context, memoryStack, pseudoRuntime, engine);
			fileProgram.Init(container, null, null, null, null, null, null, null, null, null, null, null);
			var goal = new Goal();
			goal.RelativeAppStartupFolderPath = "/";

			loopProgram = new(logger, pseudoRuntime, engine, memoryStack);
			loopProgram.Init(container, goal, null, null, null, null, null, null, null, null, null, null);
		}

		[TestMethod]
		public async Task TestListAndLoop()
		{
			string path = "Test1,000,000x10.xlsx";
			string fullPath = Path.Combine(fileSystem.RootDirectory, path);
			FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			byte[] fileBytes;
			using (BinaryReader reader = new BinaryReader(stream))
			{
				fileBytes = reader.ReadBytes((int)stream.Length);
			}
			fileSystem.AddFile(fullPath, new System.IO.Abstractions.TestingHelpers.MockFileData(fileBytes));
			await fileProgram.ReadExcelFile(path, useHeaderRow: false);



			await loopProgram.RunLoop("Sheet1", "Test", new());

		}
	}
}
