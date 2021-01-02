select 
  Error,
  spid, 
  EventClass,
  ObjectName,
  TextData,
  BinaryData,
  Handle,
* FROM sys.fn_trace_gettable ('c:\temp\1.trc', -1)