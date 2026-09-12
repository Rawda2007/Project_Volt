USE [VoltDB]
GO

/****** Object:  Table [Users].[RefreshTokens]    Script Date: 9/11/2026 2:36:39 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Users].[RefreshTokens](
	[Id] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[TokenHash] [nvarchar](500) NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[RevokedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Users].[RefreshTokens] ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [Users].[RefreshTokens] ADD  CONSTRAINT [DF_RefreshTokens_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Users].[RefreshTokens]  WITH CHECK ADD  CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY([UserId])
REFERENCES [Users].[Users] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Users].[RefreshTokens] CHECK CONSTRAINT [FK_RefreshTokens_Users]
GO


