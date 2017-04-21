# Universe.SqlTrace
Tiny library, which wraps MS SQL Server sp_trace* calls and queries to ::fn_trace_gettable into strongly types data access.

It supports column chooser and row filtering. 

### How to build from source
```
git clone https://github.com/devizer/Universe.SqlTrace.git
cd Universe.SqlTrace
call restore-build-test.cmd
```

### How to install using nuget
```
nuget install Universe.SqlTrace
```

### About API
#### Optional columns chooser
 * `Sql = 1`: *SP name or SQL Batch Text*
 * `Application = 2`: *Application Name*
 * `Database = 4`: *Database Name*
 * `ClientHost = 8`: *Client Host Name*
 * `ClientProcess = 16`: *Client Process Id*
 * `Login = 32`: *Login*
 * `ServerProcess = 64`: *SQL Server Process Id*

#### Mandatory trace columns, which are always presented in the trace session
* Duration
* CPU
* Reads
* Writes

#### Trace Session row filters
Any optional column above: Application, Database, ClientHost, ClientProcess, Login or Server Process

#### Queries to session report:
* `ReadSummaryReport()`: returns sum of mandatory trace columns and number of sql-requests
* `ReadDetailsReport()`: returns all the info from trace session, namely TraceDetailsReport instance. Its possible to get summary or group by using TraceDetailsReport instance
* `ReadGroupsReport<TKey>()`: returns sums of mandatory trace columns, grouped by one of optional trace column above.


          
          
          
          
          
          