USE [VoltDB]
GO

/****** Object:  Table [Assessment].[QuestionTranslations]    Script Date: 9/11/2026 2:21:12 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [Assessment].[QuestionTranslations](
	[QuestionId] [int] NOT NULL,
	[LanguageCode] [varchar](5) NOT NULL,
	[Field] [nvarchar](50) NOT NULL,
	[Value] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_QuestionTranslations] PRIMARY KEY CLUSTERED 
(
	[QuestionId] ASC,
	[LanguageCode] ASC,
	[Field] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [Assessment].[QuestionTranslations]  WITH CHECK ADD  CONSTRAINT [FK_QuestionTranslations_Languages] FOREIGN KEY([LanguageCode])
REFERENCES [dbo].[Languages] ([Code])
GO

ALTER TABLE [Assessment].[QuestionTranslations] CHECK CONSTRAINT [FK_QuestionTranslations_Languages]
GO

ALTER TABLE [Assessment].[QuestionTranslations]  WITH CHECK ADD  CONSTRAINT [FK_QuestionTranslations_Questions] FOREIGN KEY([QuestionId])
REFERENCES [Assessment].[Questions] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [Assessment].[QuestionTranslations] CHECK CONSTRAINT [FK_QuestionTranslations_Questions]
GO

ALTER TABLE [Assessment].[QuestionTranslations]  WITH CHECK ADD  CONSTRAINT [CK_QuestionTranslations_Field] CHECK  (([Field]=N'QuestionText'))
GO

ALTER TABLE [Assessment].[QuestionTranslations] CHECK CONSTRAINT [CK_QuestionTranslations_Field]
GO


