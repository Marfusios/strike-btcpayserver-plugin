using System.Reflection;
using System.Text.Json;

var pluginName = "BTCPayServer.Plugins.Strike";
var directories = Directory.GetDirectories("../../../../plugin");
var targetPlugins = "";
foreach (var directory in directories)
{
	try
	{
		if (!directory.EndsWith("bin"))
			continue;

		var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
		var buildConfigurationName = assemblyConfigurationAttribute?.Configuration;

		var f = $"{Path.GetFullPath(directory)}/{buildConfigurationName}/net8.0/{pluginName}.dll";
		if (File.Exists(f))
			targetPlugins += $"{f};";
		else
		{

			f = $"{Path.GetFullPath(directory)}/Debug/net8.0/{pluginName}.dll";
			if (File.Exists(f))
				targetPlugins += $"{f};";
		}
	}
	catch (Exception e)
	{
		Console.WriteLine(e);
	}
}

var content = JsonSerializer.Serialize(new
{
	DEBUG_PLUGINS = targetPlugins
});

Console.WriteLine(content);
await File.WriteAllTextAsync("../../../../submodules/BTCPayServer/BTCPayServer/appsettings.dev.json", content);
