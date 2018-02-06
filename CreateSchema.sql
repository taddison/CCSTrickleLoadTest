create database CCSTrickleLoad;
go
alter database CCSTrickleLoad set delayed_durability = forced;
go
alter database CCSTrickleLoad modify file ( name = 'CCSTrickleLoad', size = 2GB);
go
alter database CCSTrickleLoad modify file ( name = 'CCSTrickleLoad_log', size = 1GB);
go
alter database CCSTrickleLoad add filegroup CCSTrickleLoadIMO contains memory_optimized_data;
go
alter database CCSTrickleLoad add file ( name = 'CCSTrickleLoad_imo', filename = 'C:\Program Files\Microsoft SQL Server\MSSQL13.MSSQLSERVER\MSSQL\DATA\CCSTrickleLoad.imo' ) to filegroup CCSTrickleLoadIMO
go
use CCSTrickleLoad
go
create table CCSTrickle
(
	EventId uniqueidentifier not null
	,EventDateTime datetime2(2) not null
	,EventName varchar(100)
	,EventPayload nvarchar(500) null
);
go
create clustered columnstore index CCIX_Trickle on dbo.CCSTrickle with (compression_delay = 60 minutes)
go
create type dbo.TrickleType as table
(
	EventId uniqueidentifier not null primary key nonclustered
	,EventDateTime datetime2(2) not null
	,EventName varchar(100)
	,EventPayload nvarchar(500) null
) with (memory_optimized = on);
go
create or alter procedure dbo.InsertBatch
	@data dbo.TrickleType readonly
as
begin
set nocount on;

insert into dbo.CCSTrickle
(
    EventId
   ,EventDateTime
   ,EventName
   ,EventPayload
)
select d.EventId
      ,d.EventDateTime
      ,d.EventName
      ,d.EventPayload
from @data as d;
end