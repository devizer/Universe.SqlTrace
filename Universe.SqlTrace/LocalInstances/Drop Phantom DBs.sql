declare @dbname sysname
declare broken_db_list CURSOR STATIC FOR 
	Select 
		name 
	From 
		sys.databases 
	where 
		state=3
		
open broken_db_list
while 1=1 begin
  fetch next from broken_db_list into @dbname
  if @@fetch_status<>0 break
  print 'drop broken db ' + @dbname
  exec ('drop database [' + @dbname + ']')
end

close broken_db_list
deallocate broken_db_list