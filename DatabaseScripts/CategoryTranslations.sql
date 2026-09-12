USE [VoltDB]
GO

/****** Object:  Table [Assessment].[CategoryTranslations]    Script Date: 9/11/2026 2:16:53 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[CategoryTranslations](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CategoryId] [tinyint] NOT NULL,
	[LanguageCode] [nvarchar](5) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_CategoryTranslations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_CategoryTranslations_CategoryId_Language] UNIQUE NONCLUSTERED 
(
	[CategoryId] ASC,
	[LanguageCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [Assessment].[CategoryTranslations]  WITH CHECK ADD  CONSTRAINT [FK_CategoryTranslations_Categories] FOREIGN KEY([CategoryId])
REFERENCES [Assessment].[Categories] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[CategoryTranslations] CHECK CONSTRAINT [FK_CategoryTranslations_Categories]
GO

ALTER TABLE [Assessment].[CategoryTranslations]  WITH CHECK ADD  CONSTRAINT [FK_CategoryTranslations_Languages] FOREIGN KEY([LanguageCode])
REFERENCES [Assessment].[Languages] ([Code])
GO

ALTER TABLE [Assessment].[CategoryTranslations] CHECK CONSTRAINT [FK_CategoryTranslations_Languages]
GO


