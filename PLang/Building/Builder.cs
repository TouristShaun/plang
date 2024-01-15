﻿using LightInject;
using Microsoft.Extensions.Logging;
using PLang.Building.Events;
using PLang.Building.Parsers;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Utils;
using System.Diagnostics;

namespace PLang.Building
{
    public interface IBuilder
	{
		Task Start(IServiceContainer container);
	}
	public class Builder : IBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;
		private readonly ISettings settings;
		private readonly IGoalBuilder goalBuilder;
		private readonly IEventBuilder eventBuilder;
		private readonly IEventRuntime eventRuntime;
		private readonly IErrorHelper errorHelper;
		private readonly PrParser prParser;

		public Builder(ILogger logger, IPLangFileSystem fileSystem, ISettings settings, IGoalBuilder goalBuilder,
			IEventBuilder eventBuilder, IEventRuntime eventRuntime, IErrorHelper errorHelper,
			PrParser prParser)
		{

			this.fileSystem = fileSystem;
			this.logger = logger;
			this.settings = settings;
			this.goalBuilder = goalBuilder;
			this.eventBuilder = eventBuilder;
			this.eventRuntime = eventRuntime;
			this.errorHelper = errorHelper;
			this.prParser = prParser;
		}


		public async Task Start(IServiceContainer container)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				AppContext.SetSwitch("builder", true);
				var goalFiles = GoalFiles.GetGoalFilesToBuild(fileSystem, settings.GoalsPath);
				logger.LogDebug("Build Start:" + DateTime.Now.ToLongTimeString());

				var eventGoalFiles = await eventBuilder.BuildEventsPr();
				await eventRuntime.Load(container, true);

				foreach (string file in goalFiles)
				{
					await goalBuilder.BuildGoal(container, file);
				}

				goalFiles.AddRange(eventGoalFiles);
				CleanGoalFiles(goalFiles);

				logger.LogDebug("\n\nBuild done - Time:" + stopwatch.Elapsed.TotalSeconds.ToString("#,##.##") + " sec");
			}
			catch (Exception ex)
			{
				if (ex.Message != "FriendlyError")
				{
					await errorHelper.ShowFriendlyErrorMessage(ex, callBackForAskUser: async () => { await Start(container); });
				}
				
			}
		}

		private void CleanGoalFiles(List<string> goalFiles)
		{
			var dirs = fileSystem.Directory.GetDirectories(".build", "", SearchOption.AllDirectories);
			dirs = dirs.Where(dir => !dir.EndsWith(Path.DirectorySeparatorChar + "cache")).ToArray();

			var prGoalFiles = prParser.ForceLoadAllGoals();
			foreach (var dir in dirs)
			{
				var matchingGoal = prGoalFiles.FirstOrDefault(p => p.AbsolutePrFolderPath.ToLower().StartsWith(dir.ToLower()));
				if (matchingGoal == null && fileSystem.Directory.Exists(dir))
				{
					fileSystem.Directory.Delete(dir, true);
				}
			}
		}
	}


}
