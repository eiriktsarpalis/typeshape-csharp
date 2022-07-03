using BenchmarkDotNet.Running;
using System.Reflection;

var assembly = Assembly.GetExecutingAssembly();
var switcher = new BenchmarkSwitcher(assembly);
switcher.Run(args);