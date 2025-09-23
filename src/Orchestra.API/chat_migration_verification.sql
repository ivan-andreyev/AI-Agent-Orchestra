Build started...
Build succeeded.
The Entity Framework tools version '9.0.1' is older than that of the runtime '9.0.6'. Update the tools for the latest features and bug fixes. See https://aka.ms/AAc1fbw for more information.
warn: Microsoft.EntityFrameworkCore.Model.Validation[10622]
      Entity 'Agent' has a global query filter defined and is the required end of a relationship with the entity 'PerformanceMetric'. This may lead to unexpected results when the required entity is filtered out. Either configure the navigation as optional, or define matching query filters for both entities in the navigation. See https://go.microsoft.com/fwlink/?linkid=2131316 for more information.
warn: Microsoft.EntityFrameworkCore.Model.Validation[10400]
      Sensitive data logging is enabled. Log entries and exception messages may include sensitive application data; this mode should only be enabled during development.
System.NotSupportedException: Generating idempotent scripts for migrations is not currently supported for SQLite. See https://go.microsoft.com/fwlink/?LinkId=723262 for more information and examples.
   at Microsoft.EntityFrameworkCore.Sqlite.Migrations.Internal.SqliteHistoryRepository.GetEndIfScript()
   at Microsoft.EntityFrameworkCore.Migrations.Internal.Migrator.GenerateScript(String fromMigration, String toMigration, MigrationsSqlGenerationOptions options)
   at Microsoft.EntityFrameworkCore.Design.Internal.MigrationsOperations.ScriptMigration(String fromMigration, String toMigration, MigrationsSqlGenerationOptions options, String contextType)
   at Microsoft.EntityFrameworkCore.Design.OperationExecutor.ScriptMigrationImpl(String fromMigration, String toMigration, Boolean idempotent, Boolean noTransactions, String contextType)
   at Microsoft.EntityFrameworkCore.Design.OperationExecutor.ScriptMigration.<>c__DisplayClass0_0.<.ctor>b__0()
   at Microsoft.EntityFrameworkCore.Design.OperationExecutor.OperationBase.<>c__DisplayClass3_0`1.<Execute>b__0()
   at Microsoft.EntityFrameworkCore.Design.OperationExecutor.OperationBase.Execute(Action action)
Generating idempotent scripts for migrations is not currently supported for SQLite. See https://go.microsoft.com/fwlink/?LinkId=723262 for more information and examples.
