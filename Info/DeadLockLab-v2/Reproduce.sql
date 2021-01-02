select * from sys.objects
begin tran
go
CREATE TYPE dbo.IntIntSet_42 AS TABLE(Value0 Int NOT NULL,Value1 Int NOT NULL)
go
declare @myPK dbo.IntIntSet_42
go
rollback

go
If OBJECT_ID('UniqueConstraintSandbox') Is Null Create Table UniqueConstraintSandbox(id int unique)
GO
Insert UniqueConstraintSandbox(id) Values(1)
GO
Insert UniqueConstraintSandbox(id) Values(1)
GO

Declare @div int = 0
Select 5 / @div
