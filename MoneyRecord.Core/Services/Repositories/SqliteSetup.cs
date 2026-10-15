using System.Runtime.CompilerServices;
using SQLite;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// Makes SQLite3 Multiple Ciphers (the SQLite API plus encryption) the native library behind sqlite-net.
    /// sqlite-net's static constructor always picks plain SQLite (e_sqlite3), so it runs first and is then overridden.
    /// Call before the first connection opens (<see cref="DatabaseInitializer"/> does).
    /// </summary>
    public static class SqliteSetup
    {
        private static int _done;

        public static void Init()
        {
            if (Interlocked.Exchange(ref _done, 1) == 1)
                return;

            RuntimeHelpers.RunClassConstructor(typeof(SQLiteConnection).TypeHandle);
            SQLitePCL.Batteries_V2.Init();
        }
    }
}
