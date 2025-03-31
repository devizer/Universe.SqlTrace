# Universe.SqlTrace

Tiny library, which wraps MS SQL Server Profiler API (`sp_trace*` calls and queries to `::fn_trace_gettable`) into strongly types data access.

It supports column chooser and row filtering. 

Tested on SQL Server 2005 ... 2022 (incuding MS SQL LocalDB) is fully automated in linux and windows.

Targets both .NET Core, .Net Standard, and .NET Framework

Version 1.8+ Supports both System and Microsoft sql client.



### About API
#### Optional columns chooser
 * `Sql`: SP name or SQL Batch Text
 * `Application`: Application Name
 * `Database`: Database Name
 * `ClientHost`: Client Host Name
 * `ClientProcess`: Client Process Id
 * `Login`: Login
 * `ServerProcess`: SQL Server Process Id

Version 1.7+ also provides actual and compiled execution plans.

#### Mandatory trace columns, which are always presented in the trace session
* `Duration`
* `CPU`
* `Reads`
* `Writes`
* `Rows`

#### Trace Session row filters
Any optional column above could be used as row filter: Application, Database, ClientHost, ClientProcess, Login or Server Process

#### Queries to session report:
* `ReadSummaryReport()`: returns sum of mandatory trace columns and number of sql-requests
* `ReadDetailsReport()`: returns all the info from trace session, namely TraceDetailsReport instance. Its possible to get summary or group by using TraceDetailsReport instance
* `ReadGroupsReport<TKey>()`: returns sums of mandatory trace columns, grouped by one of optional trace column above.

          