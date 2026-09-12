USE [VoltDB]
GO

/****** Object:  Table [Assessment].[TopicTranslations]    Script Date: 9/11/2026 2:29:35 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[TopicTranslations](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TopicId] [int] NOT NULL,
	[LanguageCode] [nvarchar](5) NOT NULL,
	[Name] [nvarchar](200) NOT NULL,
	[Description] [nvarchar](max) NULL,
 CONSTRAINT [PK_TopicTranslations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_TopicTranslations_TopicId_Language] UNIQUE NONCLUSTERED 
(
	[TopicId] ASC,
	[LanguageCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[TopicTranslations]  WITH CHECK ADD  CONSTRAINT [FK_TopicTranslations_Languages] FOREIGN KEY([LanguageCode])
REFERENCES [Assessment].[Languages] ([Code])
GO

ALTER TABLE [Assessment].[TopicTranslations] CHECK CONSTRAINT [FK_TopicTranslations_Languages]
GO

ALTER TABLE [Assessment].[TopicTranslations]  WITH CHECK ADD  CONSTRAINT [FK_TopicTranslations_Topics] FOREIGN KEY([TopicId])
REFERENCES [Assessment].[Topics] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[TopicTranslations] CHECK CONSTRAINT [FK_TopicTranslations_Topics]
GO


