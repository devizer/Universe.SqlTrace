SET NOCOUNT ON
DECLARE @ERROR int
DECLARE @TRACE int
DECLARE @ON bit
DECLARE @maxfilesize bigint
set @maxfilesize = 16384
SET @ON = 1
declare @tracefile nvarchar(256) = 'c:\temp\1'
EXEC @ERROR = sp_trace_create @TRACE OUTPUT, 0, @tracefile, @maxfilesize
-- IF @ERROR <> 0 Begin RAISEERROR ('Failed to create trace', 16, 0) End
EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 7, N'%-- This magic comment as well as a batch are skipped by SqlTraceReader.Read call%'
-- ignore sp_reset_connection
EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 7, N'%sp_reset_connection%'
-- Statement below also works
-- EXEC @ERROR = sp_trace_setfilter @TRACE, 1, 0, 1, N'exec sp_reset_connection '
-- Batch
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 13, @ON -- Duration
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 16, @ON -- Reads
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 17, @ON -- Writes
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 18, @ON -- CPU
-- SP
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 13, @ON -- Duration
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 16, @ON -- Reads
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 17, @ON -- Writes
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 18, @ON -- CPU
-- SP Completed Only 
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 27, @ON -- EventClass
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 34, @ON -- ObjectName

EXEC @ERROR = sp_trace_setevent @TRACE, 10, 1, @ON 
EXEC @ERROR = sp_trace_setevent @TRACE, 10, 2, @ON 
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 1, @ON 
EXEC @ERROR = sp_trace_setevent @TRACE, 12, 2, @ON 


exec @ERROR = sp_trace_setevent @TRACE, 25, 13, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 16, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 17, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 18, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 27, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 1, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 2, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 34, @ON 

exec @ERROR = sp_trace_setevent @TRACE, 41, 13, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 41, 16, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 41, 17, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 41, 18, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 41, 27, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 41, 1, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 41, 2, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 41, 34, @ON 

exec @ERROR = sp_trace_setevent @TRACE, 10, 33, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 12, 33, @ON 
exec @ERROR = sp_trace_setevent @TRACE, 25, 33, @ON 

EXEC @ERROR = sp_trace_setstatus @TRACE, 1
SELECT @TRACE
