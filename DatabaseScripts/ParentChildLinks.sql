USE [VoltDB]
GO

/****** Object:  Table [Users].[ParentChildLinks]    Script Date: 9/11/2026 2:35:22 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Users].[ParentChildLinks](
	[Id] [uniqueidentifier] NOT NULL,
	[ParentUserId] [uniqueidentifier] NOT NULL,
	[ChildUserId] [uniqueidentifier] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_ParentChildLinks] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ParentChildLinks] UNIQUE NONCLUSTERED 
(
	[ParentUserId] ASC,
	[ChildUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Users].[ParentChildLinks] ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [Users].[ParentChildLinks] ADD  CONSTRAINT [DF_ParentChildLinks_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Users].[ParentChildLinks]  WITH CHECK ADD  CONSTRAINT [FK_ParentChildLinks_Child] FOREIGN KEY([ChildUserId])
REFERENCES [Users].[Users] ([Id])
GO

ALTER TABLE [Users].[ParentChildLinks] CHECK CONSTRAINT [FK_ParentChildLinks_Child]
GO

ALTER TABLE [Users].[ParentChildLinks]  WITH CHECK ADD  CONSTRAINT [FK_ParentChildLinks_Parent] FOREIGN KEY([ParentUserId])
REFERENCES [Users].[Users] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Users].[ParentChildLinks] CHECK CONSTRAINT [FK_ParentChildLinks_Parent]
GO

ALTER TABLE [Users].[ParentChildLinks]  WITH CHECK ADD  CONSTRAINT [CK_ParentChildLinks_NoSelfLink] CHECK  (([ParentUserId]<>[ChildUserId]))
GO

ALTER TABLE [Users].[ParentChildLinks] CHECK CONSTRAINT [CK_ParentChildLinks_NoSelfLink]
GO


