﻿using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Utils;
using System.Data;
using System.Data.SQLite;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PLang.Modules.DbModule
{
	public class ModuleSettings : IModuleSettings
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ILlmService aiService;
		private readonly IDbConnection db;

		public record SqlStatement(string SelectTablesAndViewsInMyDatabaseSqlStatement, string SelectColumnsFromTablesSqlStatement);

		public ModuleSettings(IPLangFileSystem fileSystem, ISettings settings, PLangAppContext context, ILlmService aiService, IDbConnection db)
		{
			this.fileSystem = fileSystem;
			this.settings = settings;
			this.context = context;
			this.aiService = aiService;
			this.db = db;
		}


		public record DataSource(string Name, string TypeFullName, string ConnectionString, string DbName, string SelectTablesAndViews, string SelectColumns, bool KeepHistory = true, bool IsDefault = false);


		public async Task CreateDataSource()
		{

			var listOfDbSupported = GetSupportedDbTypes();
			var dataSources = await GetDataSourcesByType();
			if (listOfDbSupported.Count == 1 && dataSources.Count == 0)
			{
				string dbPath = "." + Path.DirectorySeparatorChar + ".db" + Path.DirectorySeparatorChar + "data.sqlite";
				string dbAbsolutePath = Path.Join(fileSystem.RootDirectory, dbPath);

				await SetDatabaseConnectionString("data", typeof(SQLiteConnection).FullName, "data.sqlite", $"Data Source={dbAbsolutePath};Version=3;", true, true);
				return;
			}

			if (listOfDbSupported.Count == 1)
			{
				throw new AskUserSqliteName(fileSystem.RootDirectory, $"What is the name you want to give to your database?", SetDatabaseConnectionString);
			}

			var supportedDbTypes = GetSupportedDbTypesAsString();
			throw new AskUserDatabaseType(aiService, supportedDbTypes, @$"------ Datasource setup --------------
Following databases are supported:
{supportedDbTypes}. 

Type in what database you would like to use?
You can also set the connection as the default, or not to keep history

Examples you can type in:
sqllite
postresql, set as default, dont keep history

", AddDataSource);
		}

		public async Task AddDataSource(string typeFullName, string dataSourceName, string nugetCommand,
			string dataSourceConnectionStringExample, string regexToExtractDatabaseNameFromConnectionString, bool keepHistory, bool isDefault = false)
		{
			var datasources = settings.GetValues<DataSource>(this.GetType());
			if (datasources.FirstOrDefault(p => p.Name.ToLower() == dataSourceName.ToLower()) != null)
			{
				throw new AskUserDataSourceNameExists(aiService, typeFullName, dataSourceName, nugetCommand,
						dataSourceConnectionStringExample, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault,
						$"'{dataSourceName}' already exists. Give me different name if you like to add it.", AddDataSource);
			}

			if (!IsModuleInstalled(typeFullName))
			{
				var listOfDbSupported = GetSupportedDbTypesAsString();
				throw new AskUserDatabaseType(aiService, listOfDbSupported, $"{typeFullName} is not supported. Following databases are supported: {listOfDbSupported}. If you need {typeFullName}, you must install it into modules folder in your app using {nugetCommand}.", AddDataSource);
			}


			throw new AskUserDbConnectionString(dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault,
				$@"What is the connection string for {dataSourceName}? This is for {typeFullName}.

This is an example of a connection string:
	{dataSourceConnectionStringExample}

Connection string:",
				SetDatabaseConnectionString);

		}


		private async Task SetDatabaseConnectionString(string dataSourceName, string typeFullName,
					string regexToExtractDatabaseNameFromConnectionString, string databaseConnectionString,
					bool keepHistory = true, bool isDefault = false)
		{
			var dbType = GetDbType(typeFullName);
			string? error = Test(dbType, databaseConnectionString);
			if (error != null)
			{
				throw new AskUserDbConnectionString(dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault, $"Could not connect to {databaseConnectionString}. The error message was {error}.", SetDatabaseConnectionString);
			}

			var regex = new Regex(regexToExtractDatabaseNameFromConnectionString);
			var dbName = regex.Match(databaseConnectionString).Value;
			if (dbName.Contains("="))
			{
				dbName = dbName.Substring(dbName.IndexOf("=") + 1);
			}
			if (dbName.EndsWith(";"))
			{
				dbName = dbName.TrimEnd(';');
			}
			var llmQuestion = new LlmQuestion("DbModule", "",
					@$"
Give me sql statement to list all the tables and views in my database {dbName} on {typeFullName}.
Give me sql statement on how to get all column names and type in a table
Table name should be @TableName, database name is @Database if needed as parameters

Give your response in JSON, scheme
{TypeHelper.GetJsonSchema(typeof(SqlStatement))}

Be concise"
					, "");
			var statement = await aiService.Query<SqlStatement>(llmQuestion);
			if (statement == null)
			{
				throw new BuilderException("Could not get select statement for tables, views and columns. Try again.");
			}
			var dataSources = await GetAllDataSources();
			if (dataSources.Count == 0)
			{
				isDefault = true;
			}
			var dataSource = dataSources.FirstOrDefault(p => p.Name == dataSourceName);
			if (dataSource != null)
			{
				throw new AskUserDbConnectionString(dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, keepHistory, isDefault, $"{dataSourceName} alrady exists. Please choose a different name.", SetDatabaseConnectionString);
			}

			dataSource = new DataSource(dataSourceName, typeFullName, databaseConnectionString, dbName,
									statement.SelectTablesAndViewsInMyDatabaseSqlStatement, statement.SelectColumnsFromTablesSqlStatement,
									keepHistory, isDefault);

			dataSources.Add(dataSource);
			settings.SetList(this.GetType(), dataSources);
		}

		public async Task RemoveDataSource(string dataSourceName)
		{
			var dataSources = await GetDataSourcesByType();
			var dataSource = dataSources.FirstOrDefault(p => p.Name == dataSourceName);
			if (dataSource != null)
			{
				dataSources.Remove(dataSource);
			}
			settings.SetList(typeof(ModuleSettings), dataSources);
		}
		public async Task<List<DataSource>> GetDataSourcesByType()
		{
			return settings.GetValues<DataSource>(this.GetType()).Where(p => p.TypeFullName == db.GetType().FullName).ToList();
		}
		public async Task<List<DataSource>> GetAllDataSources()
		{
			return settings.GetValues<DataSource>(this.GetType()).ToList();
		}

		public async Task<DataSource?> GetDataSource(string dataSourceName)
		{
			var dataSources = await GetDataSourcesByType();
			return dataSources.FirstOrDefault(p => p.Name == dataSourceName);
		}

		public async Task<DataSource> GetCurrentDatasource()
		{
			if (context.ContainsKey(ReservedKeywords.CurrentDataSourceName))
			{
				return context[ReservedKeywords.CurrentDataSourceName] as DataSource;
			}

			var dataSources = await GetDataSourcesByType();
			if (dataSources.Count == 0)
			{
				await CreateDataSource();
				dataSources = await GetDataSourcesByType();
			}

			var dataSource = dataSources.FirstOrDefault(p => p.IsDefault);
			if (dataSource == null)
			{
				dataSource = dataSources[0];
			}
			return dataSource;
		}
		private Type GetDbType(string typeFullName)
		{
			var types = GetSupportedDbTypes();
			return types.FirstOrDefault(p => p.FullName == typeFullName);
		}

		private List<Type> GetSupportedDbTypes()
		{
			var typeHelper = new TypeHelper(fileSystem, settings);
			var types = new List<Type>();
			types.Add(db.GetType());
			if (types.FirstOrDefault(p => p == typeof(SQLiteConnection)) == null)
			{
				types.Add(typeof(SQLiteConnection));
			}
			return types;
		}
		private string GetSupportedDbTypesAsString()
		{
			var types = GetSupportedDbTypes();
			return "\n- " + string.Join("\n- ", types.Select(p => p.FullName));
		}

		private bool IsModuleInstalled(string typeFullName)
		{
			return GetDbType(typeFullName) != null;
		}

		private IDbConnection GetDbConnection(Type dbType, string connectionString)
		{
			ConstructorInfo constructor = dbType.GetConstructor(new Type[] { typeof(string) });

			// Invoke the constructor with the parameters
			object instance = constructor.Invoke(new object[] { connectionString });
			return instance as IDbConnection;
		}

		private string? Test(Type dbType, string connectionString)
		{
			if (dbType == typeof(SQLiteConnection))
			{
				var startIdx = connectionString.IndexOf('=') + 1;
				var endIdx = connectionString.IndexOf(';') - startIdx;
				string filePath = connectionString.Substring(startIdx, endIdx);

				var dbDir = Path.GetDirectoryName(filePath);
				if (!fileSystem.Directory.Exists(dbDir))
				{
					fileSystem.Directory.CreateDirectory(dbDir);
				}

				if (!fileSystem.File.Exists(filePath))
				{
					var stream = fileSystem.File.Create(filePath);
					stream.Close();
				}
			}

			var connection = GetDbConnection(dbType, connectionString);
			try
			{
				connection.Open();

			}
			catch (Exception ex)
			{
				return ex.Message;
			}
			finally
			{
				connection.Close();
			}
			return null;
		}



	}
}
