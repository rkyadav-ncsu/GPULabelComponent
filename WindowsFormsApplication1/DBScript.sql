USE [ExAlgo]
GO

/****** Object:  Table [dbo].[ExperimentalResults]    Script Date: 12/14/2015 11:04:34 AM ******/
DROP TABLE [dbo].[ExperimentalResults]
GO

/****** Object:  Table [dbo].[ExperimentalResults]    Script Date: 12/14/2015 11:04:34 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ExperimentalResults](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[d] [int] NOT NULL,
	[v] [bigint] NOT NULL,
	[cputime] [bigint] NOT NULL,
	[gputime] [bigint] NOT NULL,
	[colors] [bigint] NOT NULL,
	[totalgpu] [bigint] NOT NULL,
	[iterations] [int] NOT NULL
) ON [PRIMARY]

GO


