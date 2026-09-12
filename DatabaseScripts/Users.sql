USE [VoltDB]
GO

/****** Object:  Table [Users].[Users]    Script Date: 9/11/2026 2:37:33 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Users].[Users](
	[Id] [uniqueidentifier] NOT NULL,
	[Email] [nvarchar](255) NULL,
	[PasswordHash] [nvarchar](500) NULL,
	[FullName] [nvarchar](150) NOT NULL,
	[Role] [nvarchar](20) NOT NULL,
	[AuthProvider] [nvarchar](20) NOT NULL,
	[ProviderUserId] [nvarchar](255) NULL,
	[IsActive] [bit] NOT NULL,
	[ConvertedFromGuestAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[Age] [int] NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Users].[Users] ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [Users].[Users] ADD  CONSTRAINT [DF_Users_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO

ALTER TABLE [Users].[Users] ADD  CONSTRAINT [DF_Users_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Users].[Users]  WITH CHECK ADD  CONSTRAINT [CK_Users_AuthConsistency] CHECK  (([AuthProvider]='Email' AND [Email] IS NOT NULL AND [PasswordHash] IS NOT NULL OR [AuthProvider]='Google' AND [ProviderUserId] IS NOT NULL OR [AuthProvider]='Guest'))
GO

ALTER TABLE [Users].[Users] CHECK CONSTRAINT [CK_Users_AuthConsistency]
GO

ALTER TABLE [Users].[Users]  WITH CHECK ADD  CONSTRAINT [CK_Users_AuthProvider] CHECK  (([AuthProvider]='Guest' OR [AuthProvider]='Google' OR [AuthProvider]='Email'))
GO

ALTER TABLE [Users].[Users] CHECK CONSTRAINT [CK_Users_AuthProvider]
GO

ALTER TABLE [Users].[Users]  WITH CHECK ADD  CONSTRAINT [CK_Users_Role] CHECK  (([Role]='Admin' OR [Role]='Child' OR [Role]='Parent'))
GO

ALTER TABLE [Users].[Users] CHECK CONSTRAINT [CK_Users_Role]
GO


