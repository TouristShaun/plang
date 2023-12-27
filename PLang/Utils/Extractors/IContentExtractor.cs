﻿
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.Compiler;

namespace PLang.Utils.Extractors
{
	public interface IContentExtractor
	{
		public object Extract(string content, Type responseType);
		public T Extract<T>(string content);
		string GetRequiredResponse(Type scheme);
	}

	public class HtmlExtractor : IContentExtractor
	{
		public T Extract<T>(string content)
		{
			return (T)Extract(content, typeof(T));
		}
		public object Extract(string content, Type responseType)
		{
			var css = ExtractByType(content, "css").ToString().Trim(); 
			if (!string.IsNullOrEmpty(css))
			{
				css = "<style>" + css + "</style>\n";
			}
			var html = ExtractByType(content, "html").ToString().Trim();			
			var javascript = ExtractByType(content, "javascript").ToString().Trim();
			if (!string.IsNullOrEmpty(javascript))
			{
				if (javascript.Contains("function callGoal"))
				{
					javascript = javascript.Replace("function callGoal", "function notcalled_callGoal");
				}
				javascript = "<script>" + javascript + "</script>\n";
			}
			var result = css + html + javascript;
			return result;
		}
		public object ExtractByType(string content, string contentType = "html")
		{
			if (content.Contains($"```{contentType}"))
			{
				var regex = new Regex($"\\`\\`\\`{contentType}([^\\`\\`\\`]*)\\`\\`\\`");
				var match = regex.Match(content);
				if (match.Groups.Count > 1)
				{
					return match.Groups[1].Value ?? "";
				}
			}
			return "";
		}

		public string GetRequiredResponse(Type scheme)
		{
			return "Only write the raw html and javascript no summary, no extra text to explain, be concise";
		}
	}

	public class CSharpExtractor : IContentExtractor
	{
		public object Extract(string content, Type responseType)
		{
			var htmlExtractor = new HtmlExtractor();
			var implementation = htmlExtractor.ExtractByType(content, "csharp") as string;
			var json = htmlExtractor.ExtractByType(content, "json");

			var jsonExtractor = new JsonExtractor();
			var jsonObject = jsonExtractor.Extract(json.ToString(), typeof(CodeImplementationResponse)) as CodeImplementationResponse;

			if (implementation != null && implementation.Contains("System.IO."))
			{
				implementation = implementation.Replace("System.IO.", "PLang.SafeFileSystem.");
			}
			var ci = new CodeImplementationResponse(jsonObject.Name, implementation, jsonObject.OutParameterDefinition, jsonObject.Using, jsonObject.Assemblies, jsonObject.GoalToCallOnTrue, jsonObject.GoalToCallOnFalse);

			return ci;
		}

		public T Extract<T>(string content)
		{
			return (T)Extract(content, typeof(T));
		}

		public string GetRequiredResponse(Type scheme)
		{
			return "Only write the raw c# code and json scheme, no summary, no extra text to explain, be concise";
		}
	}

	public class JsonExtractor : IContentExtractor
	{
		public T Extract<T>(string content)
		{
			return (T)Extract(content, typeof(T));
		}

		public static string FixMalformedJson(string json)
		{

			var verbatimStringRegex = new Regex(@"@?""([^""\\]|\\.)*""", RegexOptions.Multiline);

			var newJson = verbatimStringRegex.Replace(json, match =>
			{
				string unescaped = match.Value.Trim();
				if (unescaped.StartsWith("@")) unescaped = unescaped.Substring(1);
				string pattern = @"\\(?!"")(.)";
				unescaped = Regex.Replace(unescaped, pattern, @"\\$1");

				unescaped = unescaped //.Substring(2, match.Value.Length - 3) // Remove leading @ and trailing "
											//.Replace(@"\", @"\\")
											.Replace("\"\"", "\\\"") // Replace double quotes
											.Replace("\r\n", "\\n")   // Replace newlines
											.Replace("\n", "\\n");     // Replace newlines (alternative format)
				return  unescaped; // Add enclosing quotes
			});
			return newJson;
		}

		public object Extract(string content, Type responseType)
		{
			try
			{
				try
				{
					return JsonConvert.DeserializeObject(content, responseType);
				}
				catch
				{
					var newContent = FixMalformedJson(content);
					var obj = JsonConvert.DeserializeObject(newContent, responseType);
					
					//var newJson = JsonConvert.SerializeObject(obj).Replace("[newline]", "\\n").Replace("[carreturn]", "\\r");
					return obj;
				}
			}
			catch
			{
				try
				{
					// Use a regular expression to match JSON-like objects
					Regex regex = new Regex(@"\{(?:[^{}]|(?<Level>\{)|(?<-Level>\}))+\}", RegexOptions.Multiline | RegexOptions.Compiled);
					//Regex regex = new Regex(@"(\{.*?\}|\[.*?\])", RegexOptions.Singleline | RegexOptions.Compiled);
					var newContent = FixMalformedJson(content);
					MatchCollection matches = regex.Matches(newContent);
					if (responseType.IsArray)
					{
						StringBuilder sb = new StringBuilder("[");
						foreach (Match match in matches)
						{
							if (match.Success)
							{
								if (sb.Length > 1) sb.Append(",");
								sb.Append(match.Value.ToString());
							}

						}
						sb.Append("]");
						return JsonConvert.DeserializeObject(sb.ToString(), responseType);
					}

					foreach (Match match in matches)
					{
						if (match.Success)
						{
							try
							{
								return JsonConvert.DeserializeObject(match.Value.ToString(), responseType);
							}
							catch
							{
								Console.WriteLine("Type:" + responseType);
								Console.WriteLine(match.Value.ToString());
								throw;
							}

						}
					}

					return default;
				}
				catch
				{
					throw;
				}
			}
		}

		public string GetRequiredResponse(Type scheme)
		{
			string strScheme = TypeHelper.GetJsonSchema(scheme);
			return $"You MUST respond in JSON, scheme:\r\n {strScheme}";
		}


	}
}
