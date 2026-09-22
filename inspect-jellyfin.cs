using System;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;

Console.WriteLine("=== ILibraryManager ===");
foreach (var m in typeof(ILibraryManager).GetMethods().OrderBy(x => x.Name))
{
    Console.WriteLine(m);
}

Console.WriteLine();
Console.WriteLine("=== BaseItem ===");

foreach (var p in typeof(BaseItem).GetProperties().OrderBy(x => x.Name))
{
    Console.WriteLine($"{p.PropertyType} {p.Name}");
}
