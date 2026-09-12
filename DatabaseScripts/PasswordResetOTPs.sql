USE [VoltDB]
GO

/****** Object:  Table [Users].[PasswordResetOTPs]    Script Date: 9/11/2026 2:36:00 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Users].[PasswordResetOTPs](
	[Id] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[OTPHash] [nvarchar](500) NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[VerifiedAt] [datetime2](7) NULL,
	[Attempts] [int] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_PasswordResetOTPs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Users].[PasswordResetOTPs] ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [Users].[PasswordResetOTPs] ADD  CONSTRAINT [DF_PasswordResetOTPs_Attempts]  DEFAULT ((0)) FOR [Attempts]
GO

ALTER TABLE [Users].[PasswordResetOTPs] ADD  CONSTRAINT [DF_PasswordResetOTPs_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [Users].[PasswordResetOTPs]  WITH CHECK ADD  CONSTRAINT [FK_PasswordResetOTPs_Users] FOREIGN KEY([UserId])
REFERENCES [Users].[Users] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Users].[PasswordResetOTPs] CHECK CONSTRAINT [FK_PasswordResetOTPs_Users]
GO


