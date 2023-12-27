﻿using PLang.Building.Model;

using PLang.Utils;
using PLang.Utils.Extractors;

namespace PLang.Modules.HtmlModule

{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<Instruction> Build(GoalStep step)
		{
			bool children = false;
			string childrenStr = "";
			string str = $"(Goal) {step.Goal.GoalName}\n";
			for (int i = 0;i<step.Goal.GoalSteps.Count;i++)
			{
				str +=  $"- (%step{step.Goal.GoalSteps[i].Number}%) {step.Goal.GoalSteps[i].Text}\n".PadLeft(step.Goal.GoalSteps[i].Indent, ' ');
				if (step.Goal.GoalSteps[i].Text == step.Text) children = true;
				if (children && step.Indent < step.Goal.GoalSteps[i].Indent)
				{
					childrenStr += $"{{step{step.Goal.GoalSteps[i].Number}}}\n";
				} else if (step.Goal.GoalSteps[i].Text != step.Text)
				{
					children = false;
				}
			}

			string contentToBeAdded = "";
			if (childrenStr != "")
			{
				contentToBeAdded = $"put the variable {{content}} where content should be";
			}


			SetSystem(@$"Create the html, javascript and css needed from the user command using Bootstrap 5.1. 

Goal has series of steps, it is only to provide context. Steps start with dash(-), steps can have sub steps, they are indented

Variables are defined with starting and ending %. They are case sensitive so keep them as defined

Goals are prefixed with !, they are for calling a method, e.g. Call !NewUser or reference a goal, such as Edit.goal. To call it use javascript function callGoal(name:string, parameters:object)
DO NOT generate the function callGoal, it will be provided
All object.Id or object.id are long and needs to be wrapped with single quote(')
All properties on variables are case sensitive, keep formatting defined by user.

{contentToBeAdded}  

HTML comments and <!-- --> are NOT allowed

Javascript should be vanilla Javascript. popper.min.js and bootstrap.min.js are available. 

Use @Razor templating engine for variables and to go through lists, displaying object or property, see example later

Css should be using up to date css standards. colors should be in rgb.

If a feedback is needed to the user, provide the solution also for that, if it needs javascript, provide javascript, if it needs custom css, provide css

You must return ```html, ```css, ```javascript, 
No extra text to explain, be concise
");

			var variables = GetVariablesInStep(step);
			SetAssistant($@"
### variables available ###
{variables}
### variables available ###
### For context ###
{str}
### For context ###
### Razor ###
Variables in plural are lists, singular is object. 
To access variable, prefix it with Model.
use the variable name as the name for the list and when looping through the list. 
Example for a tr in a table.

@foreach (var task in Model.tasks)
{{
    <tr>
        <td>@task.Description</td>
        <td>Due date: @task.DueDate</td>
		<td><a href=""javascript:callGoal('edit.goal', {{id:'@task.Id'}})"">Edit</a></td>
		<td><button onclick=""callGoal('delete.goal', {{id:'@task.Id'}})"">Delete</button></td>
    </tr>
}}

<h3>@Model.book.Title</h3>

@{{
    bool isUserLoggedIn = Model.isLoggedIn;
}}

@if (isUserLoggedIn)
{{
    <p>Welcome back, @user.Username!</p>
}}
else
{{
    <p>Please log in.</p>
}}
### Razor ###");

			base.SetContentExtractor(new HtmlExtractor());
			

			var result =  await base.Build<Instruction>(step);
			string html = result.Action.ToString().Replace("{content}", childrenStr);

			List<Parameter> parameters = new List<Parameter>();
			parameters.Add(new Parameter("string", "html", html));

			var gf = new GenericFunction("RenderHtml", parameters, null);
			

			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());
			var instruction = new Instruction(gf);
			step.Execute = true;
			instruction.LlmQuestion = result.LlmQuestion;
			return instruction;
		}




		}
}

