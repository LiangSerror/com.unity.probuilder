using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection;
using System.IO;
using ProBuilder2.EditorCommon;

/**
 *	Editor utility to generate the pb_EditorToolbarMenuItems class from
 *	the classes present in ProBuilder/Editor/MenuActions folder.
 */
class pb_GenerateMenuItems : Editor
{
	const string GENERATED_FILE_PATH = "Assets/ProCore/ProBuilder/Editor/EditorCore/pb_EditorToolbarMenuItems.cs";
	const string MENU_ACTIONS_PATH = "Assets/ProCore/ProBuilder/Editor/EditorCore/MenuActions/";
	const string PB_MENU_PREFIX = "Tools/ProBuilder/";

	static readonly HashSet<string> IgnoreActions = new HashSet<string>()
	{
		"SetEntityType"
	};

	static readonly Dictionary<string, string> MenuPriorityLookup = new Dictionary<string, string>()
	{
		{ "Editors",		"pb_Constant.MENU_EDITOR + 1" },
		{ "Object", 		"pb_Constant.MENU_GEOMETRY + 2" },
		{ "Geometry", 		"pb_Constant.MENU_GEOMETRY + 3" },
		{ "Interaction", 	"pb_Constant.MENU_SELECTION + 1" },
		{ "Selection", 		"pb_Constant.MENU_SELECTION + 0" }
	};

	[MenuItem("Tools/Debug/ProBuilder/Rebuild Menu Items", false, 800)]
	static void doit()
	{
		if( File.Exists(GENERATED_FILE_PATH) )
			File.Delete(GENERATED_FILE_PATH);

		StringBuilder sb = new StringBuilder();

		IEnumerable<string> actions = Directory.GetFiles(MENU_ACTIONS_PATH, "*.cs", SearchOption.AllDirectories)
			.Select(x => x.Replace("\\", "/"))
				.Where(y => !IgnoreActions.Contains(GetClassName(y)));

		sb.AppendLine(
@"/**
 *	IMPORTANT
 *
 *	This is a generated file. Any changes will be overwritten.
 *	See pb_GenerateMenuItems to make modifications.
 */

using UnityEngine;
using UnityEditor;
using ProBuilder2.Actions;
using System.Collections.Generic;

namespace ProBuilder2.EditorCommon
{
	public class pb_EditorToolbarMenuItems : Editor
	{
");
		foreach(string action in actions)
		{
			sb.AppendLine( GenerateMenuItemFunctions(action) );
		}

		sb.AppendLine("\t}");
		sb.AppendLine("}");

		File.WriteAllText(GENERATED_FILE_PATH, sb.ToString());

		Debug.Log(sb.ToString());
		pb_EditorUtility.ShowNotification("Successfully Generated\nMenu Items");

		AssetDatabase.Refresh();
	}

	/**
	 *	Generate the [MenuItem()] body for a pb_MenuAction from it's script path.
	 */
	static string GenerateMenuItemFunctions(string scriptPath)
	{
		string action = scriptPath.Replace("\\", "/").Replace(MENU_ACTIONS_PATH, "").Replace(".cs", "");
		string category = GetActionCategory(action);
		string menu_priority = GetMenuPriority(category);
		string class_name = GetClassName(action);
		string pretty_path = Regex.Replace(action, @"(\B[A-Z]+?(?=[A-Z][^A-Z])|\B[A-Z]+?(?=[^A-Z]))", " $0");

		StringBuilder sb = new StringBuilder();

		object o = System.Activator.CreateInstance( System.Type.GetType("ProBuilder2.Actions." + class_name) );
		PropertyInfo hasMenuEntryProperty = typeof(pb_MenuAction).GetProperty("hasFileMenuEntry");

		if( (bool)hasMenuEntryProperty.GetValue(o, null) == false )
			return "";

		PropertyInfo tooltipProperty = typeof(pb_MenuAction).GetProperty("tooltip");
		string shortcut = GetMenuFormattedShortcut( ((pb_TooltipContent)tooltipProperty.GetValue(o, null)).shortcut );

		/// VERIFY
		sb.Append("\t\t[MenuItem(\"");
		sb.Append(PB_MENU_PREFIX);
		sb.Append(pretty_path);
		sb.Append(" ");
		sb.Append(shortcut);
		sb.Append("\", true)]");
		sb.AppendLine("");

		sb.Append("\t\tstatic bool MenuVerify");
		sb.Append(class_name);
		sb.AppendLine("()");

		sb.AppendLine("\t\t{");

		sb.Append("\t\t\t");
		sb.Append(class_name);
		sb.Append(" instance = pb_EditorToolbarLoader.GetInstance<");
		sb.Append(class_name);
		sb.AppendLine(">();");
		sb.AppendLine("\t\t\treturn instance != null && instance.IsEnabled();");
		sb.AppendLine("\t\t}");

		sb.AppendLine("");

		/// PERFORM
		sb.Append("\t\t[MenuItem(\"");
		sb.Append(PB_MENU_PREFIX);
		sb.Append(pretty_path);
		sb.Append(" ");
		sb.Append(shortcut);
		sb.Append("\", false, ");
		sb.Append(menu_priority);
		sb.Append(")]");
		sb.AppendLine("");

		sb.Append("\t\tstatic void MenuDo");
		sb.Append(class_name);
		sb.AppendLine("()");

		sb.AppendLine("\t\t{");

		sb.Append("\t\t\t");
		sb.Append(class_name);
		sb.Append(" instance = pb_EditorToolbarLoader.GetInstance<");
		sb.Append(class_name);
		sb.AppendLine(">();");
		sb.AppendLine("\t\t\tif(instance != null)");
		sb.AppendLine("\t\t\t\tpb_EditorUtility.ShowNotification(instance.DoAction().notification);");
		sb.AppendLine("\t\t}");

		return sb.ToString();
	}

	static string GetClassName(string scriptPath)
	{
		string file = Path.GetFileName(scriptPath);
		if(file != null)
			return file.Replace(".cs", "");
		return null;
	}

	static string GetActionCategory(string scriptPath)
	{
		string[] split = scriptPath.Split('/');
		return split[0];
	}

	static string GetMenuPriority(string category)
	{
		string menu_priority;

		if( !MenuPriorityLookup.TryGetValue(category, out menu_priority) )
			menu_priority = "0";

		return menu_priority;
	}

	static string GetMenuFormattedShortcut(string shortcut)
	{
		string res = "";
		string[] keys = shortcut.Split('+');
		bool inSceneShortcut = true;

		foreach(string s in keys)
		{
			if(s.Contains(pb_Constant.CMD_SUPER) || s.Contains("Control") )
			{
				res += "%";
				inSceneShortcut = false;
			}
			else if(s.Contains(pb_Constant.CMD_OPTION) || s.Contains(pb_Constant.CMD_ALT) || s.Contains("Alt")  || s.Contains("Option") )
			{
				res += "&";
				inSceneShortcut = false;
			}
			else if(s.Contains(pb_Constant.CMD_SHIFT) || s.Contains("Shift") )
			{
				res += "#";
				inSceneShortcut = false;
			}
			else
				res += s.Trim();
		}

		if(res.Length > 0 && inSceneShortcut)
			res = string.Format(" [{0}]", res);

		return res;
	}
}