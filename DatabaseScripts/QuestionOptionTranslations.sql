USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuestionOptionTranslations]    Script Date: 9/11/2026 2:19:38 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuestionOptionTranslations](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[QuestionOptionId] [int] NOT NULL,
	[LanguageCode] [nvarchar](5) NOT NULL,
	[OptionText] [nvarchar](max) NULL,
 CONSTRAINT [PK_QuestionOptionTranslations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_QuestionOptionTranslations_OptionId_Language] UNIQUE NONCLUSTERED 
(
	[QuestionOptionId] ASC,
	[LanguageCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuestionOptionTranslations]  WITH CHECK ADD  CONSTRAINT [FK_QuestionOptionTranslations_Languages] FOREIGN KEY([LanguageCode])
REFERENCES [Assessment].[Languages] ([Code])
GO

ALTER TABLE [Assessment].[QuestionOptionTranslations] CHECK CONSTRAINT [FK_QuestionOptionTranslations_Languages]
GO

ALTER TABLE [Assessment].[QuestionOptionTranslations]  WITH CHECK ADD  CONSTRAINT [FK_QuestionOptionTranslations_QuestionOptions] FOREIGN KEY([QuestionOptionId])
REFERENCES [Assessment].[QuestionOptions] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuestionOptionTranslations] CHECK CONSTRAINT [FK_QuestionOptionTranslations_QuestionOptions]
GO


