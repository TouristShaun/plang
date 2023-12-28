﻿using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.ComponentModel;
using System.Dynamic;

namespace PLang.Modules.LlmModule
{
    [Description("Ask LLM a question and recieve and answer")]
	public class Program : BaseProgram
	{
		private readonly ILlmService llmService;
		private readonly MemoryStack memoryStack;
		private readonly VariableHelper variableHelper;

		public Program(ILlmService llmService, MemoryStack memoryStack, VariableHelper variableHelper) : base()
		{
			this.llmService = llmService;
			this.memoryStack = memoryStack;
			this.variableHelper = variableHelper;
		}

		public record AskLlmResponse(string Result);

		[Description("")]
		public async Task AskLlm(
			string scheme = "",
			string? system = null, string? assistant = null, string? user = null,
			string model = "gpt-4",
			double temperature = 0,
			double topP = 0,
			double frequencyPenalty = 0.0,
			double presencePenalty = 0.0,
			int maxLength = 4000,
			bool cacheResponse = true,
			string llmResponseType = "text")
		{
			if (llmResponseType == "text")
			{
				llmService.Extractor = new TextExtractor();
			}
			else if (llmResponseType == "json")
			{
				system += $"\n\nYou MUST respond in JSON, scheme: {scheme}";
			} else
			{
				llmService.Extractor = new GenericExtractor(llmResponseType); 
			}

			user = LoadVariables(user) ?? "";
			system = LoadVariables(system);
			assistant = LoadVariables(assistant);
			
			var llmQuestion = new LlmQuestion("LlmModule", system, user, assistant, model, cacheResponse);
			llmQuestion.maxLength = maxLength;
			llmQuestion.temperature = temperature;
			llmQuestion.top_p = topP;
			llmQuestion.frequencyPenalty = frequencyPenalty;
			llmQuestion.presencePenalty = presencePenalty;
			
			var response = await llmService.Query(llmQuestion, typeof(ExpandoObject));

			if (scheme.StartsWith("{") && scheme.EndsWith("}"))
			{
				var variables = scheme.Replace("{", "").Replace("}", "").Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				var objResult = (IDictionary<string, object>)response;
				foreach (var variable in variables)
				{
					string varName = (variable.Contains(":")) ? variable.Substring(0, variable.IndexOf(":")) : variable;
					if (objResult.TryGetValue(varName, out object? val))
					{
						memoryStack.Put(varName, val);
					}
				}
			}
			if (function.ReturnValue != null && !string.IsNullOrEmpty(function.ReturnValue.VariableName))
			{
				memoryStack.Put(function.ReturnValue.VariableName, response);
			}

			llmService.Extractor = new JsonExtractor();
		}

		private string? LoadVariables(string? content)
		{
			if (content == null) return null;

			var variables = variableHelper.GetVariables(content);
			foreach (var variable in variables)
			{
				var varValue = memoryStack.Get(variable.Key);
				if (varValue != null)
				{
					content = content.Replace(variable.OriginalKey, varValue.ToString());
				}
			}
			return content;
		}



	}
}
