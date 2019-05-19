select * from sys.objects
begin tran
go
CREATE TYPE dbo.IntIntSet_42 AS TABLE(Value0 Int NOT NULL,Value1 Int NOT NULL)
go
declare @myPK dbo.IntIntSet_42
go
rollback

