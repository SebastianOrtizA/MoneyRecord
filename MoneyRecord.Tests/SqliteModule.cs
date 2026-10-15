using System.Runtime.CompilerServices;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Tests;

internal static class SqliteModule
{
    /// <summary>
    /// The app's SQLite (with encryption) for every test, also the ones that open a connection without
    /// <see cref="DatabaseInitializer"/>: switching the native library later would strand open connections.
    /// </summary>
    [ModuleInitializer]
    internal static void Init() => SqliteSetup.Init();
}
